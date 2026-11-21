using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace EventCheckIn.Server;

public record CheckInDto(
    string Id, string FullName, string Email, string EventName, DateTime CheckedInAt,
    string? KioskName, string? PhotoFile, List<string> DocumentFiles, string? SignatureFile,
    DateTime? ReceivedAt = null, List<FieldAnswer>? Answers = null, List<string>? DocumentNames = null,
    int EventVersion = 0);

public record KioskDto(string Name, DateTime PairedAt, DateTime? LastSeen);

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
            """);

        // Schema upgrades for databases created by earlier versions.
        AddColumnIfMissing(c, "checkins", "answers", "TEXT");
        AddColumnIfMissing(c, "checkins", "document_names", "TEXT");
        AddColumnIfMissing(c, "checkins", "event_version", "INTEGER");
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

    public void AddKiosk(string token, string name)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO kiosks(token_hash,name,paired_at) VALUES($h,$n,$t)";
        cmd.Parameters.AddWithValue("$h", Hash(token));
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Returns the kiosk name if the token is valid, and records last-seen.</summary>
    public string? ValidateToken(string token)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE kiosks SET last_seen=$t WHERE token_hash=$h RETURNING name";
        cmd.Parameters.AddWithValue("$h", Hash(token));
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        return cmd.ExecuteScalar() as string;
    }

    public List<KioskDto> GetKiosks()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT name,paired_at,last_seen FROM kiosks ORDER BY paired_at";
        using var r = cmd.ExecuteReader();
        var list = new List<KioskDto>();
        while (r.Read())
            list.Add(new KioskDto(r.GetString(0), DateTime.Parse(r.GetString(1)).ToLocalTime(),
                r.IsDBNull(2) ? null : DateTime.Parse(r.GetString(2)).ToLocalTime()));
        return list;
    }

    public void RemoveAllKiosks()
    {
        using var c = Open();
        Exec(c, "DELETE FROM kiosks");
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
              document_files,signature_file,received_at,answers,document_names,event_version)
            VALUES($id,$n,$e,$ev,$at,$k,$p,$docs,$s,$rx,$ans,$dn,$evv)
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
        cmd.ExecuteNonQuery();
    }

    public List<CheckInDto> GetCheckIns()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT id,full_name,email,event_name,checked_in_at,kiosk_name,photo_file,document_files,
                   signature_file,received_at,answers,document_names,event_version
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
                r.IsDBNull(12) ? 0 : r.GetInt32(12)));
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
