using System.Text.Json;
using EventCheckIn.Models;

namespace EventCheckIn.Services;

/// <summary>
/// The event currently shown on the kiosk. Downloaded from the host and cached on the device,
/// so the kiosk keeps working with the last-known event while offline.
/// </summary>
public class EventConfigService
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    readonly string _folder = Path.Combine(FileSystem.AppDataDirectory, "event");
    string ConfigFile => Path.Combine(_folder, "event.json");

    public EventConfig Current { get; private set; }
    /// <summary>Local path of the cached logo, or null if the event has none.</summary>
    public string? LogoPath { get; private set; }

    /// <summary>Raised (on the main thread) when a new event version has been downloaded.</summary>
    public event EventHandler? Changed;

    public EventConfigService()
    {
        Directory.CreateDirectory(_folder);
        Current = LoadCached() ?? EventConfig.CreateDefault();
        LogoPath = FindLogo(Current.Version);
    }

    EventConfig? LoadCached()
    {
        try { return File.Exists(ConfigFile) ? JsonSerializer.Deserialize<EventConfig>(File.ReadAllText(ConfigFile), Json) : null; }
        catch { return null; }
    }

    // Versioned file name so image caches never show a stale logo.
    string LogoFileFor(int version) => Path.Combine(_folder, $"logo_v{version}.img");
    string? FindLogo(int version) => Current.HasLogo && File.Exists(LogoFileFor(version)) ? LogoFileFor(version) : null;

    /// <summary>Fetches the host's event; downloads the logo if needed. Returns true if anything changed.</summary>
    public async Task<bool> RefreshAsync(ServerConnection server, CancellationToken ct = default)
    {
        var latest = await server.GetEventAsync(ct);
        if (latest is null) return false;

        bool logoMissing = latest.HasLogo && !File.Exists(LogoFileFor(latest.Version));
        if (latest.Version == Current.Version && !logoMissing) return false;

        if (latest.HasLogo && logoMissing)
        {
            var bytes = await server.DownloadLogoAsync(ct);
            if (bytes is null) return false; // try again next sync
            await File.WriteAllBytesAsync(LogoFileFor(latest.Version), bytes, ct);
        }

        await File.WriteAllTextAsync(ConfigFile, JsonSerializer.Serialize(latest, Json), ct);
        foreach (var old in Directory.GetFiles(_folder, "logo_v*.img").Where(f => f != LogoFileFor(latest.Version)))
            try { File.Delete(old); } catch { }

        Current = latest;
        LogoPath = latest.HasLogo ? LogoFileFor(latest.Version) : null;
        MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(this, EventArgs.Empty));
        return true;
    }

    /// <summary>Forget the host's event (e.g. after unpairing).</summary>
    public void ResetToDefault()
    {
        try { if (File.Exists(ConfigFile)) File.Delete(ConfigFile); } catch { }
        Current = EventConfig.CreateDefault();
        LogoPath = null;
        MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(this, EventArgs.Empty));
    }
}
