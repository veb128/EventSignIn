namespace EventCheckIn.Models;

// Mirrors the host's event configuration (EventCheckIn.Server/EventConfig.cs).

public class EventConfig
{
    public int Version { get; set; }
    public string EventName { get; set; } = "Event Check-In";
    public string WelcomeMessage { get; set; } = "Welcome! Please check in.";
    public bool HasLogo { get; set; }
    public bool CollectPhoto { get; set; } = true;
    public string TermsText { get; set; } =
        "I confirm the information and documents provided are accurate and agree to the event terms.";
    public List<FieldDef> Fields { get; set; } = new();
    public List<DocumentDef> Documents { get; set; } = new();

    /// <summary>Used until the kiosk has been paired and received the host's event.</summary>
    public static EventConfig CreateDefault() => new()
    {
        Version = 0,
        Fields =
        {
            new FieldDef { Id = "email", Label = "Email", Type = FieldTypes.Text, Placeholder = "jane@example.com",
                           Validation = new ValidationDef { Kind = ValidationKinds.Email } }
        },
        Documents = { new DocumentDef { Id = "doc", Name = "Document", Instructions = "Hold the document flat inside the frame.", Required = false } }
    };
}

public static class FieldTypes
{
    public const string Text = "text", Radio = "radio", Checkbox = "checkbox", Dropdown = "dropdown";
}

public static class ValidationKinds
{
    public const string None = "none", Email = "email", Phone = "phone", Number = "number", Regex = "regex";
}

public class FieldDef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = FieldTypes.Text;
    public bool Required { get; set; }
    public string Placeholder { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public ValidationDef Validation { get; set; } = new();
}

public class ValidationDef
{
    public string Kind { get; set; } = ValidationKinds.None;
    public string Pattern { get; set; } = "";
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string Message { get; set; } = "";
}

public class DocumentDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Instructions { get; set; } = "";
    public bool Required { get; set; } = true;
}

public class FieldAnswer
{
    public string FieldId { get; set; } = "";
    public string Label { get; set; } = "";
    public List<string> Values { get; set; } = new();
}
