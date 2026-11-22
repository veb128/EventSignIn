using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace EventCheckIn.Server;

/// <summary>
/// One-time pairing: the host shows a 6-digit PIN; a kiosk that enters it receives a
/// long random token used for all later requests. Repeated wrong PINs lock out that IP briefly.
/// </summary>
public class PairingService(Store store)
{
    const int MaxFailures = 5;
    static readonly TimeSpan Lockout = TimeSpan.FromMinutes(2);

    readonly ConcurrentDictionary<string, (int Count, DateTime Until)> _failures = new();
    public string Pin { get; private set; } = NewPin();

    public void RotatePin() => Pin = NewPin();

    static string NewPin() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public (bool Ok, string? Token, string? Error) TryPair(string? pin, string? kioskName, bool isAdmin, IPAddress? from)
    {
        var key = from?.ToString() ?? "?";
        if (_failures.TryGetValue(key, out var f) && f.Count >= MaxFailures && f.Until > DateTime.UtcNow)
            return (false, null, "Too many attempts. Try again in a couple of minutes.");

        if (pin is null || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(pin.Trim()), System.Text.Encoding.UTF8.GetBytes(Pin)))
        {
            _failures.AddOrUpdate(key, _ => (1, DateTime.UtcNow + Lockout),
                (_, old) => (old.Count + 1, DateTime.UtcNow + Lockout));
            return (false, null, "Incorrect PIN.");
        }

        _failures.TryRemove(key, out _);
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var name = string.IsNullOrWhiteSpace(kioskName) ? $"Kiosk {key}" : kioskName.Trim()[..Math.Min(kioskName.Trim().Length, 60)];
        store.AddKiosk(token, name, isAdmin);
        return (true, token, null);
    }

    public (bool Ok, string? Error) VerifyAdminRolePin(string? suppliedPin, string? expectedPinHash, IPAddress? from)
    {
        var key = "docpin:" + (from?.ToString() ?? "?");
        if (_failures.TryGetValue(key, out var f) && f.Count >= MaxFailures && f.Until > DateTime.UtcNow)
            return (false, "Too many failed PIN attempts. Try again in 2 minutes.");

        if (string.IsNullOrEmpty(expectedPinHash))
            return (false, "No PIN has been configured for this admin role on the host.");

        var hash = string.IsNullOrWhiteSpace(suppliedPin) ? "" : Store.Hash(suppliedPin.Trim());
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(hash), System.Text.Encoding.UTF8.GetBytes(expectedPinHash)))
        {
            _failures.AddOrUpdate(key, _ => (1, DateTime.UtcNow + Lockout),
                (_, old) => (old.Count + 1, DateTime.UtcNow + Lockout));
            return (false, "Incorrect PIN.");
        }

        _failures.TryRemove(key, out _);
        return (true, null);
    }
}
