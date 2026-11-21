namespace EventCheckIn.Server;

public class ServerOptions
{
    /// <summary>Port the kiosks connect to (HTTP, LAN only).</summary>
    public int Port { get; set; } = 5080;

    /// <summary>Friendly name shown to kiosks during discovery.</summary>
    public string Name { get; set; } = Environment.MachineName;

    /// <summary>Where the database and uploaded files live. Defaults to the user's local app-data folder.</summary>
    public string? DataDirectory { get; set; }

    public HotspotOptions Hotspot { get; set; } = new();

    public string ResolveDataDirectory() =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EventCheckInServer")
            : DataDirectory!;
}

public class HotspotOptions
{
    /// <summary>Try to start a Wi-Fi hotspot on startup (Windows / Linux only).</summary>
    public bool Enabled { get; set; } = true;
    public string Ssid { get; set; } = "EventCheckIn";
    /// <summary>WPA2 passphrase (8+ chars). If empty, one is generated once and saved.</summary>
    public string? Password { get; set; }
    /// <summary>Linux only: Wi-Fi interface (e.g. wlan0). Empty = let NetworkManager choose.</summary>
    public string? Interface { get; set; }
}
