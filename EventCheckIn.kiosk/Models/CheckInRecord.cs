namespace EventCheckIn.Models;

/// <summary>A completed (or in-progress) attendee check-in.</summary>
public class CheckInRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public int EventVersion { get; set; }
    public DateTime CheckedInAt { get; set; }
    public string? KioskName { get; set; }

    /// <summary>Answers to the host-configured questions.</summary>
    public List<FieldAnswer> Answers { get; set; } = new();

    // File names, relative to the record's folder
    public string? PhotoFile { get; set; }
    public List<string> DocumentFiles { get; set; } = new();
    /// <summary>Name of each scanned document, same order as DocumentFiles.</summary>
    public List<string> DocumentNames { get; set; } = new();
    /// <summary>DocumentDef.Id of each scanned document, same order as DocumentFiles.</summary>
    public List<string> DocumentIds { get; set; } = new();
    /// <summary>Per-document access level returned by the host for Admin tablets ("allowed", "pin_required", "restricted").</summary>
    public List<string> DocumentAccess { get; set; } = new();
    public string? AdminRoleName { get; set; }
    public string? SignatureFile { get; set; }
    public string? QrCodeFile { get; set; }

    /// <summary>True once the host server has confirmed it received this check-in.</summary>
    public bool Synced { get; set; }
    public DateTime? SyncedAt { get; set; }
}
