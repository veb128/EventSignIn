using System.Text.Json;
using System.Text.RegularExpressions;

namespace EventCheckIn.Server;

// ---------------- Event configuration (shared shape with the kiosk app) ----------------

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

    public static EventConfig CreateDefault() => new()
    {
        Version = 1,
        Fields =
        {
            new FieldDef { Id = "email", Label = "Email", Type = FieldTypes.Text, Placeholder = "jane@example.com",
                           Validation = new ValidationDef { Kind = ValidationKinds.Email } },
        },
        Documents = { new DocumentDef { Id = "id", Name = "Photo ID", Instructions = "Driver's licence or passport, photo side up.", Required = true } }
    };
}

public static class FieldTypes
{
    public const string Text = "text", Radio = "radio", Checkbox = "checkbox", Dropdown = "dropdown";
    public static readonly string[] All = [Text, Radio, Checkbox, Dropdown];
}

public static class ValidationKinds
{
    public const string None = "none", Email = "email", Phone = "phone", Number = "number", Regex = "regex";
    public static readonly string[] All = [None, Email, Phone, Number, Regex];
}

public class FieldDef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = FieldTypes.Text;
    public bool Required { get; set; }
    public string Placeholder { get; set; } = "";
    public List<string> Options { get; set; } = new();          // radio / checkbox / dropdown
    public ValidationDef Validation { get; set; } = new();      // text only
}

public class ValidationDef
{
    public string Kind { get; set; } = ValidationKinds.None;
    public string Pattern { get; set; } = "";                   // for Kind = regex
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public double? Min { get; set; }                            // for Kind = number
    public double? Max { get; set; }
    public string Message { get; set; } = "";                   // custom error text
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

// ---------------- Persistence + validation ----------------

public partial class EventService(Store store)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    static readonly string[] LogoExtensions = [".png", ".jpg"];
    const long MaxLogoBytes = 5 * 1024 * 1024;
    readonly object _gate = new();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")] private static partial Regex IdPattern();

    public EventConfig Get()
    {
        var json = store.GetOrCreateSetting("event_config", () => JsonSerializer.Serialize(EventConfig.CreateDefault(), Json));
        var cfg = JsonSerializer.Deserialize<EventConfig>(json, Json) ?? EventConfig.CreateDefault();
        cfg.HasLogo = LogoPath() is not null;
        return cfg;
    }

    /// <summary>Validates and saves; bumps Version so kiosks pick up the change.</summary>
    public (bool Ok, List<string> Errors, EventConfig? Saved) Save(EventConfig incoming)
    {
        var errors = Validate(incoming);
        if (errors.Count > 0) return (false, errors, null);
        lock (_gate)
        {
            incoming.Version = Get().Version + 1;
            incoming.HasLogo = LogoPath() is not null;
            store.SetSetting("event_config", JsonSerializer.Serialize(incoming, Json));
            return (true, errors, incoming);
        }
    }

    void BumpVersion()
    {
        lock (_gate)
        {
            var cfg = Get();
            cfg.Version++;
            store.SetSetting("event_config", JsonSerializer.Serialize(cfg, Json));
        }
    }

    public static List<string> Validate(EventConfig c)
    {
        var e = new List<string>();
        c.EventName = c.EventName?.Trim() ?? "";
        if (c.EventName.Length is 0 or > 120) e.Add("Event name is required (max 120 characters).");
        c.WelcomeMessage ??= ""; c.TermsText ??= "";
        if (c.WelcomeMessage.Length > 500) e.Add("Welcome message is too long (max 500).");
        if (c.TermsText.Length > 2000) e.Add("Terms text is too long (max 2000).");
        c.Fields ??= new(); c.Documents ??= new();
        if (c.Fields.Count > 50) e.Add("Too many fields (max 50).");
        if (c.Documents.Count > 20) e.Add("Too many documents (max 20).");

        var ids = new HashSet<string>();
        foreach (var f in c.Fields)
        {
            f.Label = f.Label?.Trim() ?? "";
            var name = f.Label.Length > 0 ? $"Field \"{f.Label}\"" : "A field";
            if (f.Label.Length is 0 or > 120) e.Add($"{name}: label is required (max 120).");
            if (!IdPattern().IsMatch(f.Id ?? "")) e.Add($"{name}: invalid id.");
            else if (!ids.Add("f:" + f.Id)) e.Add($"{name}: duplicate id \"{f.Id}\".");
            if (!FieldTypes.All.Contains(f.Type)) e.Add($"{name}: unknown type \"{f.Type}\".");
            f.Options = (f.Options ?? new()).Select(o => o?.Trim() ?? "").Where(o => o.Length > 0).Distinct().ToList();
            f.Validation ??= new();
            if (f.Type != FieldTypes.Text)
            {
                if (f.Options.Count < 1) e.Add($"{name}: add at least one option.");
                f.Validation = new ValidationDef();
            }
            else
            {
                f.Options = new();
                var v = f.Validation;
                if (!ValidationKinds.All.Contains(v.Kind)) e.Add($"{name}: unknown validation \"{v.Kind}\".");
                if (v.MinLength < 0 || v.MaxLength < 1 || (v.MinLength > v.MaxLength)) e.Add($"{name}: invalid length limits.");
                if (v.Min > v.Max) e.Add($"{name}: minimum is greater than maximum.");
                if (v.Kind == ValidationKinds.Regex)
                {
                    try { _ = new Regex(v.Pattern ?? "", RegexOptions.None, TimeSpan.FromMilliseconds(200)); }
                    catch (ArgumentException ex) { e.Add($"{name}: invalid pattern – {ex.Message}"); }
                    if (string.IsNullOrWhiteSpace(v.Pattern)) e.Add($"{name}: pattern is required for custom validation.");
                }
            }
        }
        foreach (var d in c.Documents)
        {
            d.Name = d.Name?.Trim() ?? ""; d.Instructions = d.Instructions?.Trim() ?? "";
            var name = d.Name.Length > 0 ? $"Document \"{d.Name}\"" : "A document";
            if (d.Name.Length is 0 or > 80) e.Add($"{name}: name is required (max 80).");
            if (!IdPattern().IsMatch(d.Id ?? "")) e.Add($"{name}: invalid id.");
            else if (!ids.Add("d:" + d.Id)) e.Add($"{name}: duplicate id \"{d.Id}\".");
        }
        return e;
    }

    // ---------------- logo ----------------

    public string? LogoPath() =>
        LogoExtensions.Select(x => Path.Combine(store.DataDir, "logo" + x)).FirstOrDefault(File.Exists);

    public async Task<string?> SaveLogoAsync(IFormFile file)
    {
        if (file.Length == 0 || file.Length > MaxLogoBytes) return "Logo must be an image under 5 MB.";
        var header = new byte[8];
        await using (var s = file.OpenReadStream()) _ = await s.ReadAsync(header);

        // Trust the file contents, not the name: PNG or JPEG only.
        string? ext = header[..4].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) ? ".png"
                    : header[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }) ? ".jpg" : null;
        if (ext is null) return "Logo must be a PNG or JPEG image.";

        DeleteLogoFiles();
        await using (var fs = File.Create(Path.Combine(store.DataDir, "logo" + ext)))
            await file.CopyToAsync(fs);
        BumpVersion();
        return null;
    }

    public void DeleteLogo() { DeleteLogoFiles(); BumpVersion(); }

    void DeleteLogoFiles()
    {
        foreach (var x in LogoExtensions)
        {
            var p = Path.Combine(store.DataDir, "logo" + x);
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
