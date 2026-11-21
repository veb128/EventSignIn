using System.Diagnostics;
using System.Security.Cryptography;

namespace EventCheckIn.Server;

/// <summary>
/// Starts an offline Wi-Fi hotspot for the kiosks where the OS allows it:
///   Linux   – NetworkManager (nmcli). Host address is usually 10.42.0.1.
///   Windows – Mobile Hotspot via the WinRT tethering API. Host address 192.168.137.1.
///   macOS   – no public API; use Internet Sharing or a travel router (status explains).
/// </summary>
public class HotspotService(ServerOptions options, Store store, ILogger<HotspotService> log) : IHostedService
{
    const string LinuxConnectionName = "EventCheckIn-Hotspot";

    public string Status { get; private set; } = "Not started";
    public bool Running { get; private set; }
    public string Ssid => options.Hotspot.Ssid;
    public string Password { get; private set; } = "";

    public async Task StartAsync(CancellationToken ct)
    {
        Password = !string.IsNullOrWhiteSpace(options.Hotspot.Password) && options.Hotspot.Password!.Length >= 8
            ? options.Hotspot.Password!
            : store.GetOrCreateSetting("hotspot_password", GeneratePassword);

        if (!options.Hotspot.Enabled)
        {
            Status = "Disabled in settings – kiosks must join an existing network with this computer.";
            return;
        }

        try
        {
            if (OperatingSystem.IsLinux()) await StartLinuxAsync(ct);
            else if (OperatingSystem.IsWindows()) await StartWindowsAsync(ct);
            else if (OperatingSystem.IsMacOS())
                Status = "macOS can't start a hotspot automatically. Turn on System Settings → General → Sharing → " +
                         "Internet Sharing (share to Wi-Fi), or connect this Mac and the kiosks to a private travel router.";
            else Status = "Automatic hotspot not supported on this OS.";
        }
        catch (Exception ex)
        {
            Status = $"Hotspot failed: {ex.Message}";
            log.LogWarning(ex, "Hotspot start failed");
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (!Running) return;
        try
        {
            if (OperatingSystem.IsLinux())
                await RunAsync("nmcli", ["connection", "down", LinuxConnectionName], null, ct);
            else if (OperatingSystem.IsWindows())
                await RunAsync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", WindowsScript("stop")], null, ct);
        }
        catch (Exception ex) { log.LogWarning(ex, "Hotspot stop failed"); }
        Running = false;
    }

    async Task StartLinuxAsync(CancellationToken ct)
    {
        var args = new List<string> { "device", "wifi", "hotspot", "con-name", LinuxConnectionName,
                                      "ssid", options.Hotspot.Ssid, "password", Password };
        if (!string.IsNullOrWhiteSpace(options.Hotspot.Interface))
            args.InsertRange(3, ["ifname", options.Hotspot.Interface!]);

        var (code, output) = await RunAsync("nmcli", args, null, ct);
        Running = code == 0;
        Status = Running ? $"Hotspot \"{Ssid}\" running (NetworkManager). Host address is usually 10.42.0.1."
                         : $"nmcli failed ({code}): {output.Trim()} – you may need to run with sudo or install NetworkManager.";
    }

    async Task StartWindowsAsync(CancellationToken ct)
    {
        var env = new Dictionary<string, string> { ["ECI_SSID"] = options.Hotspot.Ssid, ["ECI_PASS"] = Password };
        var (code, output) = await RunAsync("powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", WindowsScript("start")], env, ct);
        var result = output.Trim();
        Running = code == 0 && result.EndsWith("Success", StringComparison.OrdinalIgnoreCase);
        Status = Running ? $"Mobile Hotspot \"{Ssid}\" running. Host address is 192.168.137.1."
                         : $"Mobile Hotspot could not start: {result}. Turn it on manually in Settings → Network & internet → Mobile hotspot.";
    }

    // Windows PowerShell 5.1 can call the WinRT tethering API directly. Values come from env vars (no string injection).
    static string WindowsScript(string action) => $$"""
        $ErrorActionPreference='Stop'
        Add-Type -AssemblyName System.Runtime.WindowsRuntime
        $ext=[System.WindowsRuntimeSystemExtensions].GetMethods()|?{$_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1}
        $asOp=($ext|?{$_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'})[0]
        $asAct=($ext|?{$_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction'})[0]
        function AwaitOp($op,[Type]$t){$x=$asOp.MakeGenericMethod($t).Invoke($null,@($op));$x.Wait(-1)|Out-Null;$x.Result}
        function AwaitAct($a){$x=$asAct.Invoke($null,@($a));$x.Wait(-1)|Out-Null}
        [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime]|Out-Null
        [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]|Out-Null
        [Windows.Networking.NetworkOperators.NetworkOperatorTetheringAccessPointConfiguration,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]|Out-Null
        $p=[Windows.Networking.Connectivity.NetworkInformation]::GetInternetConnectionProfile()
        if(-not $p){$p=[Windows.Networking.Connectivity.NetworkInformation]::GetConnectionProfiles()|Select-Object -First 1}
        if(-not $p){throw 'Windows needs at least one network adapter profile (Wi-Fi or Ethernet) to host a hotspot.'}
        $m=[Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($p)
        $rt=[Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult]
        if('{{action}}' -eq 'stop'){ (AwaitOp ($m.StopTetheringAsync()) $rt).Status; exit }
        $c=New-Object Windows.Networking.NetworkOperators.NetworkOperatorTetheringAccessPointConfiguration
        $c.Ssid=$env:ECI_SSID; $c.Passphrase=$env:ECI_PASS
        AwaitAct ($m.ConfigureAccessPointAsync($c))
        (AwaitOp ($m.StartTetheringAsync()) $rt).Status
        """;

    static string GeneratePassword()
    {
        const string chars = "abcdefghjkmnpqrstuvwxyz23456789"; // no look-alike characters
        return string.Concat(Enumerable.Range(0, 12).Select(i =>
            (i is 4 or 8 ? "-" : "") + chars[RandomNumberGenerator.GetInt32(chars.Length)]));
    }

    static async Task<(int Code, string Output)> RunAsync(string file, IEnumerable<string> args,
        Dictionary<string, string>? env, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (env is not null) foreach (var (k, v) in env) psi.Environment[k] = v;

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}");
        var stdout = p.StandardOutput.ReadToEndAsync(ct);
        var stderr = p.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await p.WaitForExitAsync(timeout.Token);
        return (p.ExitCode, (await stdout) + (await stderr));
    }
}
