using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace EventCheckIn.Server;

public record CheckInDto(
    string Id, string FullName, string Email, string EventName, DateTime CheckedInAt,
    string? KioskName, string? PhotoFile, List<string> DocumentFiles, string? SignatureFile,
    DateTime? ReceivedAt = null, List<FieldAnswer>? Answers = null, List<string>? DocumentNames = null,
    int EventVersion = 0, string? QrCodeFile = "qrcode.png", List<string>? DocumentIds = null);

public record KioskDto(string Id, string Name, DateTime PairedAt, DateTime? LastSeen, bool ShowSettings, bool IsAdmin, string? AdminRoleId, bool ModeLocked);

public record AdminUserDto(
    string Id, string Username, string DisplayName, string AccessLevelId,
    bool HasPin, bool MustResetPin, bool IsLocked, DateTime CreatedAt, DateTime? LastLoginAt);

public record AdminUserRecord(
    string Id, string Username, string DisplayName, string PinHash, string AccessLevelId,
    bool MustResetPin, bool IsLocked, int FailedAttempts, DateTime? LockoutUntil,
    DateTime CreatedAt, DateTime? LastLoginAt);

/// <summary>SQLite metadata + files on disk. Everything stays on this machine.</summary>
public class Store
{
    readonly string _connStr;
    public string DataDir { get; }
    public string FilesDir { get; }

    public Store(ServerOptions options)
    {
        DataDir = options.ResolveDataDirectory();
        FilesDir = Path.Combine(DataDir, "checkins");
        Directory.CreateDirectory(FilesDir);
        _connStr = new SqliteConnectionStringBuilder { DataSource = Path.Combine(DataDir, "eventcheckin.db") }.ToString();
    }

    SqliteConnection Open()
    {
        var c = new SqliteConnection(_connStr);
        c.Open();
        return c;
    }

    public void Initialize()
    {
        using var c = Open();
        Exec(c, """
            CREATE TABLE IF NOT EXISTS checkins(
              id TEXT PRIMARY KEY, full_name TEXT NOT NULL, email TEXT, event_name TEXT,
              checked_in_at TEXT NOT NULL, kiosk_name TEXT, photo_file TEXT,
              document_files TEXT NOT NULL, signature_file TEXT, received_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS kiosks(
              token_hash TEXT PRIMARY KEY, name TEXT NOT NULL, paired_at TEXT NOT NULL, last_seen TEXT);
            CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS admin_users(
              id TEXT PRIMARY KEY, username TEXT NOT NULL UNIQUE COLLATE NOCASE, display_name TEXT NOT NULL,
              pin_hash TEXT NOT NULL, access_level_id TEXT NOT NULL, must_reset_pin INTEGER NOT NULL DEFAULT 1,
              is_locked INTEGER NOT NULL DEFAULT 0, failed_attempts INTEGER NOT NULL DEFAULT 0,
              lockout_until TEXT, created_at TEXT NOT NULL, last_login_at TEXT);
            CREATE TABLE IF NOT EXISTS admin_sessions(
              session_hash TEXT PRIMARY KEY, user_id TEXT NOT NULL, kiosk_hash TEXT NOT NULL,
              created_at TEXT NOT NULL, expires_at TEXT NOT NULL);
            """);

        // Schema upgrades for databases created by earlier versions.
        AddColumnIfMissing(c, "checkins", "answers", "TEXT");
        AddColumnIfMissing(c, "checkins", "document_names", "TEXT");
        AddColumnIfMissing(c, "checkins", "document_ids", "TEXT");
        AddColumnIfMissing(c, "checkins", "event_version", "INTEGER");
        AddColumnIfMissing(c, "checkins", "qrcode_file", "TEXT");
        AddColumnIfMissing(c, "kiosks", "show_settings", "INTEGER DEFAULT 0");
        AddColumnIfMissing(c, "kiosks", "is_admin", "INTEGER DEFAULT 0");
        AddColumnIfMissing(c, "kiosks", "admin_role_id", "TEXT");
        AddColumnIfMissing(c, "kiosks", "mode_locked", "INTEGER DEFAULT 0");
    }

    static void AddColumnIfMissing(SqliteConnection c, string table, string column, string type)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name=$c";
        cmd.Parameters.AddWithValue("$c", column);
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) Exec(c, $"ALTER TABLE {table} ADD COLUMN {column} {type}");
    }

    public void SetSetting(string key, string value)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    // ---------- settings ----------
    public string GetOrCreateSetting(string key, Func<string> create)
    {
        using var c = Open();
        using var get = c.CreateCommand();
        get.CommandText = "SELECT value FROM settings WHERE key=$k";
        get.Parameters.AddWithValue("$k", key);
        if (get.ExecuteScalar() is string v) return v;

        var value = create();
        using var ins = c.CreateCommand();
        ins.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v)";
        ins.Parameters.AddWithValue("$k", key);
        ins.Parameters.AddWithValue("$v", value);
        ins.ExecuteNonQuery();
        return value;
    }

    // ---------- kiosks ----------
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public void AddKiosk(string token, string name, bool isAdmin = false)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO kiosks(token_hash,name,paired_at,is_admin) VALUES($h,$n,$t,$a)";
        cmd.Parameters.AddWithValue("$h", Hash(token));
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$a", isAdmin ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Returns the kiosk name, show_settings, is_admin, admin_role_id, and mode_locked if the token is valid, and records last-seen.</summary>
    public (string Name, bool ShowSettings, bool IsAdmin, string? AdminRoleId, bool ModeLocked)? ValidateToken(string token)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE kiosks SET last_seen=$t WHERE token_hash=$h RETURNING name, COALESCE(show_settings, 0), COALESCE(is_admin, 0), admin_role_id, COALESCE(mode_locked, 0)";
        cmd.Parameters.AddWithValue("$h", Hash(token));
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetInt32(1) != 0, r.GetInt32(2) != 0, r.IsDBNull(3) ? null : r.GetString(3), r.GetInt32(4) != 0) : null;
    }

    public List<KioskDto> GetKiosks()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT token_hash,name,paired_at,last_seen,COALESCE(show_settings, 0),COALESCE(is_admin, 0),admin_role_id,COALESCE(mode_locked, 0) FROM kiosks ORDER BY paired_at";
        using var r = cmd.ExecuteReader();
        var list = new List<KioskDto>();
        while (r.Read())
            list.Add(new KioskDto(r.GetString(0), r.GetString(1), DateTime.Parse(r.GetString(2)).ToLocalTime(),
                r.IsDBNull(3) ? null : DateTime.Parse(r.GetString(3)).ToLocalTime(),
                r.GetInt32(4) != 0, r.GetInt32(5) != 0, r.IsDBNull(6) ? null : r.GetString(6), r.GetInt32(7) != 0));
        return list;
    }

    public void SetKioskShowSettings(string tokenHash, bool show)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE kiosks SET show_settings=$s WHERE token_hash=$h";
        cmd.Parameters.AddWithValue("$s", show ? 1 : 0);
        cmd.Parameters.AddWithValue("$h", tokenHash);
        cmd.ExecuteNonQuery();
    }

    public void SetKioskAdminMode(string tokenHash, bool isAdmin, bool modeLocked)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            UPDATE kiosks SET is_admin=$a, mode_locked=$l WHERE token_hash=$h;
            DELETE FROM admin_sessions WHERE kiosk_hash=$h AND $a=0;
            """;
        cmd.Parameters.AddWithValue("$a", isAdmin ? 1 : 0);
        cmd.Parameters.AddWithValue("$l", modeLocked ? 1 : 0);
        cmd.Parameters.AddWithValue("$h", tokenHash);
        cmd.ExecuteNonQuery();
    }

    public void SetKioskAdminRole(string tokenHash, string? roleId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE kiosks SET admin_role_id=$r WHERE token_hash=$h";
        cmd.Parameters.AddWithValue("$r", (object?)roleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$h", tokenHash);
        cmd.ExecuteNonQuery();
    }

    public void SetAllKiosksShowSettings(bool show)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE kiosks SET show_settings=$s";
        cmd.Parameters.AddWithValue("$s", show ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void RemoveAllKiosks()
    {
        using var c = Open();
        Exec(c, "DELETE FROM admin_sessions; DELETE FROM kiosks;");
    }

    // ---------- admin users & sessions (SQLite) ----------
    public List<AdminUserDto> GetAdminUsers()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT id,username,display_name,access_level_id,pin_hash,must_reset_pin,is_locked,created_at,last_login_at
            FROM admin_users ORDER BY username
            """;
        using var r = cmd.ExecuteReader();
        var list = new List<AdminUserDto>();
        while (r.Read())
        {
            list.Add(new AdminUserDto(
                r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
                !string.IsNullOrEmpty(r.GetString(4)), r.GetInt32(5) != 0, r.GetInt32(6) != 0,
                DateTime.Parse(r.GetString(7)).ToLocalTime(),
                r.IsDBNull(8) ? null : DateTime.Parse(r.GetString(8)).ToLocalTime()));
        }
        return list;
    }

    public AdminUserRecord? GetAdminUserByUsername(string username)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT id,username,display_name,pin_hash,access_level_id,must_reset_pin,is_locked,
                   failed_attempts,lockout_until,created_at,last_login_at
            FROM admin_users WHERE username=$u
            """;
        cmd.Parameters.AddWithValue("$u", username.Trim());
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadAdminUserRecord(r) : null;
    }

    public AdminUserRecord? GetAdminUserById(string id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT id,username,display_name,pin_hash,access_level_id,must_reset_pin,is_locked,
                   failed_attempts,lockout_until,created_at,last_login_at
            FROM admin_users WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadAdminUserRecord(r) : null;
    }

    static AdminUserRecord ReadAdminUserRecord(SqliteDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
        r.GetInt32(5) != 0, r.GetInt32(6) != 0, r.GetInt32(7),
        r.IsDBNull(8) ? null : DateTime.Parse(r.GetString(8)).ToUniversalTime(),
        DateTime.Parse(r.GetString(9)).ToLocalTime(),
        r.IsDBNull(10) ? null : DateTime.Parse(r.GetString(10)).ToLocalTime());

    public (bool Ok, string? Error) CreateAdminUser(string username, string displayName, string accessLevelId, string? initialPin, bool mustResetPin)
    {
        username = username?.Trim() ?? "";
        displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim();
        if (username.Length is 0 or > 40) return (false, "Username is required (max 40 characters).");
        if (displayName.Length > 80) return (false, "Display name is too long (max 80 characters).");
        if (GetAdminUserByUsername(username) is not null) return (false, $"Username \"{username}\" already exists.");

        var pinHash = string.IsNullOrWhiteSpace(initialPin) ? "" : Hash(initialPin.Trim());
        if (string.IsNullOrEmpty(pinHash)) mustResetPin = true;

        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO admin_users(id,username,display_name,pin_hash,access_level_id,must_reset_pin,is_locked,failed_attempts,created_at)
            VALUES($id,$u,$d,$p,$a,$m,0,0,$t)
            """;
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("$u", username);
        cmd.Parameters.AddWithValue("$d", displayName);
        cmd.Parameters.AddWithValue("$p", pinHash);
        cmd.Parameters.AddWithValue("$a", accessLevelId);
        cmd.Parameters.AddWithValue("$m", mustResetPin ? 1 : 0);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        return (true, null);
    }

    public void SetAdminUserAccessLevel(string userId, string accessLevelId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE admin_users SET access_level_id=$a WHERE id=$id";
        cmd.Parameters.AddWithValue("$a", accessLevelId);
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    public void SetAdminUserLocked(string userId, bool locked)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            UPDATE admin_users SET is_locked=$l, failed_attempts=0, lockout_until=NULL WHERE id=$id;
            DELETE FROM admin_sessions WHERE user_id=$id AND $l=1;
            """;
        cmd.Parameters.AddWithValue("$l", locked ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    public void ForceAdminUserPinReset(string userId, string? tempPin = null)
    {
        var pinHash = string.IsNullOrWhiteSpace(tempPin) ? "" : Hash(tempPin.Trim());
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            UPDATE admin_users SET pin_hash=$p, must_reset_pin=1, is_locked=0, failed_attempts=0, lockout_until=NULL WHERE id=$id;
            DELETE FROM admin_sessions WHERE user_id=$id;
            """;
        cmd.Parameters.AddWithValue("$p", pinHash);
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateAdminUserPin(string userId, string newPin)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            UPDATE admin_users SET pin_hash=$p, must_reset_pin=0, failed_attempts=0, lockout_until=NULL WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$p", Hash(newPin.Trim()));
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    public void RecordAdminLoginFailure(string userId, int maxAttempts)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            UPDATE admin_users
            SET failed_attempts = failed_attempts + 1,
                is_locked = CASE WHEN failed_attempts + 1 >= $max THEN 1 ELSE is_locked END
            WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$max", maxAttempts);
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    public void RecordAdminLoginSuccess(string userId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            UPDATE admin_users SET failed_attempts=0, lockout_until=NULL, last_login_at=$t WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteAdminUser(string userId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM admin_sessions WHERE user_id=$id; DELETE FROM admin_users WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    public string CreateAdminSession(string userId, string kioskTokenHash)
    {
        var sessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            DELETE FROM admin_sessions WHERE kiosk_hash=$kh OR expires_at < $now;
            INSERT INTO admin_sessions(session_hash,user_id,kiosk_hash,created_at,expires_at)
            VALUES($sh,$u,$kh,$now,$exp);
            """;
        cmd.Parameters.AddWithValue("$sh", Hash(sessionToken));
        cmd.Parameters.AddWithValue("$u", userId);
        cmd.Parameters.AddWithValue("$kh", kioskTokenHash);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$exp", DateTime.UtcNow.AddHours(12).ToString("O"));
        cmd.ExecuteNonQuery();
        return sessionToken;
    }

    public void DeleteAdminSession(string sessionToken)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM admin_sessions WHERE session_hash=$sh";
        cmd.Parameters.AddWithValue("$sh", Hash(sessionToken));
        cmd.ExecuteNonQuery();
    }

    public AdminUserRecord? ValidateAdminSession(string? sessionToken, string kioskTokenHash)
    {
        if (string.IsNullOrWhiteSpace(sessionToken)) return null;
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT u.id,u.username,u.display_name,u.pin_hash,u.access_level_id,u.must_reset_pin,u.is_locked,
                   u.failed_attempts,u.lockout_until,u.created_at,u.last_login_at
            FROM admin_sessions s
            JOIN admin_users u ON u.id = s.user_id
            WHERE s.session_hash=$sh AND s.kiosk_hash=$kh AND s.expires_at > $now AND u.is_locked=0 AND u.must_reset_pin=0
            """;
        cmd.Parameters.AddWithValue("$sh", Hash(sessionToken.Trim()));
        cmd.Parameters.AddWithValue("$kh", kioskTokenHash);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadAdminUserRecord(r) : null;
    }

    // ---------- check-ins ----------
    public bool Exists(string id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM checkins WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() is not null;
    }

    public void Insert(CheckInDto d)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO checkins(id,full_name,email,event_name,checked_in_at,kiosk_name,photo_file,
              document_files,signature_file,received_at,answers,document_names,event_version,qrcode_file,document_ids)
            VALUES($id,$n,$e,$ev,$at,$k,$p,$docs,$s,$rx,$ans,$dn,$evv,$qr,$di)
            """;
        cmd.Parameters.AddWithValue("$id", d.Id);
        cmd.Parameters.AddWithValue("$n", d.FullName);
        cmd.Parameters.AddWithValue("$e", d.Email);
        cmd.Parameters.AddWithValue("$ev", d.EventName);
        cmd.Parameters.AddWithValue("$at", d.CheckedInAt.ToString("O"));
        cmd.Parameters.AddWithValue("$k", (object?)d.KioskName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$p", (object?)d.PhotoFile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$docs", JsonSerializer.Serialize(d.DocumentFiles));
        cmd.Parameters.AddWithValue("$s", (object?)d.SignatureFile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rx", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$ans", JsonSerializer.Serialize(d.Answers ?? new()));
        cmd.Parameters.AddWithValue("$dn", JsonSerializer.Serialize(d.DocumentNames ?? new()));
        cmd.Parameters.AddWithValue("$evv", d.EventVersion);
        cmd.Parameters.AddWithValue("$qr", (object?)d.QrCodeFile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$di", JsonSerializer.Serialize(d.DocumentIds ?? new()));
        cmd.ExecuteNonQuery();
    }

    public CheckInDto? GetCheckIn(string id) => GetCheckIns().FirstOrDefault(c => c.Id == id);

    public List<CheckInDto> GetCheckIns()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT id,full_name,email,event_name,checked_in_at,kiosk_name,photo_file,document_files,
                   signature_file,received_at,answers,document_names,event_version,qrcode_file,document_ids
            FROM checkins ORDER BY checked_in_at DESC
            """;
        using var r = cmd.ExecuteReader();
        var list = new List<CheckInDto>();
        while (r.Read())
        {
            list.Add(new CheckInDto(
                r.GetString(0), r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2),
                r.IsDBNull(3) ? "" : r.GetString(3), DateTime.Parse(r.GetString(4)),
                r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6),
                JsonSerializer.Deserialize<List<string>>(r.GetString(7)) ?? new(),
                r.IsDBNull(8) ? null : r.GetString(8), DateTime.Parse(r.GetString(9)).ToLocalTime(),
                r.IsDBNull(10) ? new() : JsonSerializer.Deserialize<List<FieldAnswer>>(r.GetString(10)) ?? new(),
                r.IsDBNull(11) ? new() : JsonSerializer.Deserialize<List<string>>(r.GetString(11)) ?? new(),
                r.IsDBNull(12) ? 0 : r.GetInt32(12),
                r.IsDBNull(13) ? "qrcode.png" : r.GetString(13),
                r.IsDBNull(14) ? new() : JsonSerializer.Deserialize<List<string>>(r.GetString(14)) ?? new()));
        }
        return list;
    }

    public string FolderFor(string id) => Path.Combine(FilesDir, id);

    static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
