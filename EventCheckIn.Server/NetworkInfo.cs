using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EventCheckIn.Server;

public static class NetworkInfo
{
    /// <summary>IPv4 addresses of this machine's active, non-loopback network adapters.</summary>
    public static IEnumerable<string> LanAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .Where(a => !a.StartsWith("169.254."))
            .Distinct();
}
