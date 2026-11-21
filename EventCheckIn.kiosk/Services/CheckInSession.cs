using EventCheckIn.Models;

namespace EventCheckIn.Services;

public record CapturedDocument(string DocumentId, string Name, byte[] Image);

/// <summary>Holds the data for the attendee currently moving through the check-in flow.</summary>
public class CheckInSession
{
    /// <summary>True until the attendee starts; the welcome form is rebuilt when fresh.</summary>
    public bool IsFresh { get; set; } = true;

    public EventConfig? Event { get; set; }        // snapshot taken when the attendee starts
    public string FullName { get; set; } = string.Empty;
    public List<FieldAnswer> Answers { get; set; } = new();
    public byte[]? Photo { get; set; }
    public List<CapturedDocument> Documents { get; } = new();
    public byte[]? Signature { get; set; }

    public CheckInRecord? LastCompleted { get; set; }

    public string EventName => Event?.EventName ?? string.Empty;

    /// <summary>Best-effort email for the dashboard: the first answer that is validated as an email.</summary>
    public string Email =>
        Event?.Fields.Where(f => f.Validation.Kind == ValidationKinds.Email)
            .Select(f => Answers.FirstOrDefault(a => a.FieldId == f.Id)?.Values.FirstOrDefault())
            .FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? string.Empty;

    public void Reset()
    {
        IsFresh = true;
        Event = null;
        FullName = string.Empty;
        Answers = new();
        Photo = Signature = null;
        Documents.Clear();
    }
}
