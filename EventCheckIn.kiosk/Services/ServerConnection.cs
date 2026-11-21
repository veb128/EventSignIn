using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using EventCheckIn.Models;

namespace EventCheckIn.Services;

public record FoundServer(string Name, string BaseUrl)
{
    public string Display => $"{Name}  ({BaseUrl})";
}

/// <summary>
/// Talks to the EventCheckIn host on the local network. Nothing is sent to the internet:
/// the only addresses ever contacted are private LAN addresses found by discovery or typed in.
/// </summary>
public class ServerConnection
{
    public const int DefaultPort = 5080;
    const string Product = "EventCheckIn";

    // Typical host addresses: Windows Mobile Hotspot, Linux NetworkManager hotspot, macOS Internet Sharing.
    static readonly string[] KnownHotspotHosts = ["192.168.137.1", "10.42.0.1", "192.168.2.1"];

    static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string? BaseUrl
    {
        get => Preferences.Get("server_url", null);
        private set => Preferences.Set("server_url", value);
    }
    public string? ServerName
    {
        get => Preferences.Get("server_name", null);
        private set => Preferences.Set("server_name", value);
    }
    public string KioskName
    {
        get => Preferences.Get("kiosk_name", DeviceInfo.Name);
        set => Preferences.Set("kiosk_name", value);
    }
    string? _token;
    bool _tokenLoaded;

    // Token is kept in the platform secure store (Keychain / Android Keystore); falls back to
    // app preferences if secure storage isn't available (e.g. unsigned Mac debug builds).
    string? Token
    {
        get
        {
            if (_tokenLoaded) return _token;
            try { _token = Task.Run(() => SecureStorage.GetAsync("server_token")).GetAwaiter().GetResult(); }
            catch { _token = null; }
            _token ??= Preferences.Get("server_token_fallback", null);
            _tokenLoaded = true;
            return _token;
        }
        set
        {
            _token = value;
            _tokenLoaded = true;
            Preferences.Remove("server_token_fallback");
            try
            {
                if (value is null) SecureStorage.Remove("server_token");
                else Task.Run(() => SecureStorage.SetAsync("server_token", value)).GetAwaiter().GetResult();
            }
            catch
            {
                if (value is not null) Preferences.Set("server_token_fallback", value);
            }
        }
    }

    public bool IsPaired => !string.IsNullOrEmpty(BaseUrl) && !string.IsNullOrEmpty(Token);

    // ---------------- discovery ----------------

    /// <summary>Finds hosts: saved/known addresses first, then a sweep of each local /24 subnet.</summary>
    public async Task<List<FoundServer>> DiscoverAsync(CancellationToken ct = default)
    {
        var locals = LocalIPv4().ToList();
        var quick = new List<string>();
        if (BaseUrl is not null && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var saved)) quick.Add(saved.Host);
        quick.AddRange(locals.Select(ip => Prefix(ip) + ".1"));
        quick.AddRange(KnownHotspotHosts);

        var found = await ProbeManyAsync(quick.Distinct(), TimeSpan.FromSeconds(1.5), ct);
        if (found.Count > 0) return found;

        // Host isn't the gateway (e.g. travel router): sweep the subnet.
        var sweep = locals.SelectMany(ip => Enumerable.Range(1, 254).Select(i => $"{Prefix(ip)}.{i}"))
                          .Except(locals).Except(quick);
        return await ProbeManyAsync(sweep, TimeSpan.FromSeconds(1.2), ct);
    }

    /// <summary>Checks a typed-in address such as "192.168.1.20" or "192.168.1.20:5080".</summary>
    public async Task<FoundServer?> ProbeAddressAsync(string address, CancellationToken ct = default)
    {
        address = address.Trim();
        if (!address.Contains("://")) address = "http://" + address;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return null;
        var port = uri.IsDefaultPort && !address.Contains($":{uri.Port}") ? DefaultPort : uri.Port;
        return await ProbeAsync($"http://{uri.Host}:{port}", TimeSpan.FromSeconds(3), ct);
    }

    async Task<List<FoundServer>> ProbeManyAsync(IEnumerable<string> hosts, TimeSpan timeout, CancellationToken ct)
    {
        var results = new List<FoundServer>();
        using var gate = new SemaphoreSlim(48);
        var tasks = hosts.Select(async h =>
        {
            await gate.WaitAsync(ct);
            try
            {
                if (await ProbeAsync($"http://{h}:{DefaultPort}", timeout, ct) is { } s)
                    lock (results) results.Add(s);
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
        return results.DistinctBy(r => r.BaseUrl).ToList();
    }

    static async Task<FoundServer?> ProbeAsync(string baseUrl, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            var info = await Http.GetFromJsonAsync<ServerInfo>($"{baseUrl}/api/info", Json, cts.Token);
            return info?.Product == Product ? new FoundServer(info.Name ?? baseUrl, baseUrl) : null;
        }
        catch { return null; }
    }

    static IEnumerable<string> LocalIPv4()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .Where(IsPrivate)
                .Distinct()
                .ToList();
        }
        catch { return []; }
    }

    // Only ever scan private ranges – never public internet addresses.
    static bool IsPrivate(string ip) =>
        ip.StartsWith("10.") || ip.StartsWith("192.168.") ||
        (ip.StartsWith("172.") && int.TryParse(ip.Split('.')[1], out var b) && b is >= 16 and <= 31);

    static string Prefix(string ip) => ip[..ip.LastIndexOf('.')];

    // ---------------- pairing ----------------

    public async Task<(bool Ok, string? Error)> PairAsync(FoundServer server, string pin, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            var resp = await Http.PostAsJsonAsync($"{server.BaseUrl}/api/pair",
                new { pin, kioskName = KioskName }, Json, cts.Token);
            var body = await resp.Content.ReadFromJsonAsync<PairResponse>(Json, cts.Token);
            if (!resp.IsSuccessStatusCode || string.IsNullOrEmpty(body?.Token))
                return (false, body?.Error ?? $"Server returned {(int)resp.StatusCode}");

            BaseUrl = server.BaseUrl;
            ServerName = server.Name;
            Token = body.Token;
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public void Unpair()
    {
        Token = null;
        Preferences.Remove("server_url");
        Preferences.Remove("server_name");
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        if (!IsPaired) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/ping");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var resp = await Http.SendAsync(req, cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ---------------- event ----------------

    public async Task<EventConfig?> GetEventAsync(CancellationToken ct = default)
    {
        var bytes = await GetAuthorizedAsync("/api/event", TimeSpan.FromSeconds(10), ct);
        if (bytes is null) return null;
        try { return JsonSerializer.Deserialize<EventConfig>(bytes, Json); }
        catch (JsonException) { return null; }
    }

    public Task<byte[]?> DownloadLogoAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync("/api/event/logo", TimeSpan.FromSeconds(30), ct);

    async Task<byte[]?> GetAuthorizedAsync(string path, TimeSpan timeout, CancellationToken ct)
    {
        if (!IsPaired) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{path}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            using var resp = await Http.SendAsync(req, cts.Token);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsByteArrayAsync(cts.Token) : null;
        }
        catch { return null; }
    }

    // ---------------- upload ----------------

    public enum UploadResult { Ok, Offline, Unauthorized, Rejected }

    public async Task<UploadResult> UploadAsync(CheckInRecord record, CheckInStorage storage, CancellationToken ct = default)
    {
        if (!IsPaired) return UploadResult.Unauthorized;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(JsonSerializer.Serialize(record, Json)), "record");
        AddFile(form, storage.GetPath(record, record.PhotoFile), "photo", "image/jpeg");
        foreach (var d in record.DocumentFiles)
            AddFile(form, storage.GetPath(record, d), "documents", "image/jpeg");
        AddFile(form, storage.GetPath(record, record.SignatureFile), "signature", "image/png");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/checkins") { Content = form };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            using var resp = await Http.SendAsync(req, cts.Token);
            return resp.IsSuccessStatusCode ? UploadResult.Ok
                : resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ? UploadResult.Unauthorized
                : UploadResult.Rejected;
        }
        catch { return UploadResult.Offline; }
    }

    static void AddFile(MultipartFormDataContent form, string? path, string field, string mime)
    {
        if (path is null || !File.Exists(path)) return;
        var content = new ByteArrayContent(File.ReadAllBytes(path));
        content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(content, field, Path.GetFileName(path));
    }

    record ServerInfo(string? Product, string? Name, int Version);
    record PairResponse(string? Token, string? Error);
}
