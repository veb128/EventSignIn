using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventCheckIn.Models;
using EventCheckIn.Services;

namespace EventCheckIn.ViewModels;

/// <summary>Decides the next screen based on what the host's event asks for.</summary>
public static class Flow
{
    public static Task AfterDetails(CheckInSession s) =>
        s.Event!.CollectPhoto ? Go(nameof(Views.PhotoPage)) : AfterPhoto(s);

    public static Task AfterPhoto(CheckInSession s) =>
        s.Event!.Documents.Count > 0 ? Go(nameof(Views.DocumentsPage)) : Go(nameof(Views.SignaturePage));

    static Task Go(string route) => Shell.Current.GoToAsync(route);

    public static ImageSource? Image(byte[]? b) => b is null ? null : ImageSource.FromStream(() => new MemoryStream(b));
}

// ---------- Step 1: attendee details (host-configured form) ----------
public partial class WelcomeViewModel(CheckInSession session, SyncService sync, EventConfigService events) : ObservableObject
{
    public SyncService Sync => sync;
    public ObservableCollection<FieldInput> Fields { get; } = new();

    [ObservableProperty] string eventName = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasWelcome))] string welcomeMessage = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLogo))] ImageSource? logo;
    [ObservableProperty] string fullName = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNameError))] string nameError = string.Empty;

    public bool HasLogo => Logo is not null;
    public bool HasWelcome => !string.IsNullOrWhiteSpace(WelcomeMessage);
    public bool HasNameError => !string.IsNullOrEmpty(NameError);

    /// <summary>Builds the form from the current event (fresh attendee) or restores it (coming back).</summary>
    public void Load()
    {
        var cfg = session.IsFresh || session.Event is null ? events.Current : session.Event;
        EventName = cfg.EventName;
        WelcomeMessage = cfg.WelcomeMessage;
        Logo = events.LogoPath is { } p ? ImageSource.FromFile(p) : null;

        FullName = session.IsFresh ? string.Empty : session.FullName;
        NameError = string.Empty;
        Fields.Clear();
        foreach (var f in cfg.Fields)
            Fields.Add(new FieldInput(f, session.IsFresh ? null : session.Answers.FirstOrDefault(a => a.FieldId == f.Id)));
    }

    /// <summary>Host published a new event: refresh the form if nobody is mid check-in.</summary>
    public void OnEventChanged()
    {
        if (session.IsFresh) Load();
    }

    partial void OnFullNameChanged(string value)
    {
        if (HasNameError && !string.IsNullOrWhiteSpace(value)) NameError = string.Empty;
    }

    [RelayCommand]
    async Task Start()
    {
        bool ok = true;
        if (string.IsNullOrWhiteSpace(FullName)) { NameError = "Please enter your full name."; ok = false; }
        foreach (var f in Fields) ok &= f.Validate();
        if (!ok) return;

        session.Event = session.IsFresh || session.Event is null ? events.Current : session.Event;
        session.FullName = FullName.Trim();
        session.Answers = Fields.Select(f => f.ToAnswer()).ToList();
        session.IsFresh = false;
        await Flow.AfterDetails(session);
    }

    [RelayCommand]
    Task OpenSettings() => Shell.Current.GoToAsync(nameof(Views.SettingsPage));

    [RelayCommand]
    Task ViewCheckIns() => Shell.Current.GoToAsync(nameof(Views.CheckInListPage));
}

// ---------- Step 2: attendee photo ----------
public partial class PhotoViewModel(CheckInSession session) : ObservableObject
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPhoto), nameof(IsCapturing))]
    ImageSource? photo;

    public bool HasPhoto => Photo is not null;
    public bool IsCapturing => Photo is null;

    public void Load() => Photo = Flow.Image(session.Photo);

    public void SetPhoto(byte[] bytes)
    {
        session.Photo = bytes;
        Photo = Flow.Image(bytes);
    }

    [RelayCommand]
    void Retake()
    {
        session.Photo = null;
        Photo = null;
    }

    [RelayCommand]
    Task Next() => Flow.AfterPhoto(session);
}

// ---------- Step 3: guided document scans ----------
public partial class DocumentsViewModel(CheckInSession session) : ObservableObject
{
    public ObservableCollection<DocumentStep> Steps { get; } = new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Prompt), nameof(Instructions), nameof(CanSkip), nameof(ScanButtonText))]
    DocumentStep? current;
    [ObservableProperty] bool canContinue;

    public string Prompt => Current is null ? "" : $"Scan: {Current.Def.Name}";
    public string Instructions => Current?.Def.Instructions ?? "";
    public bool CanSkip => Current is { Def.Required: false, HasImage: false };
    public string ScanButtonText => Current?.HasImage == true ? "📄 Rescan" : "📄 Scan";

    public void Load()
    {
        Steps.Clear();
        foreach (var d in session.Event!.Documents)
        {
            var step = new DocumentStep(d);
            if (session.Documents.FirstOrDefault(c => c.DocumentId == d.Id) is { } cap) step.SetImage(cap.Image);
            Steps.Add(step);
        }
        Select(Steps.FirstOrDefault(s => !s.HasImage) ?? Steps.FirstOrDefault());
        Update();
    }

    public void Capture(byte[] image)
    {
        if (Current is null) return;
        Current.SetImage(image);
        // Keep the session's list in the event's document order.
        session.Documents.Clear();
        session.Documents.AddRange(Steps.Where(s => s.HasImage).Select(s => new CapturedDocument(s.Def.Id, s.Def.Name, s.Image!)));
        Advance();
    }

    [RelayCommand]
    void Select(DocumentStep? step)
    {
        foreach (var s in Steps) s.IsCurrent = s == step;
        Current = step;
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(ScanButtonText));
    }

    [RelayCommand]
    void Skip()
    {
        if (Current is null) return;
        Current.Skipped = true;
        Advance();
    }

    void Advance()
    {
        var idx = Current is null ? -1 : Steps.IndexOf(Current);
        var next = Steps.Skip(idx + 1).FirstOrDefault(s => !s.HasImage && !s.Skipped)
                   ?? Steps.FirstOrDefault(s => !s.HasImage && !s.Skipped);
        Select(next ?? Current);
        Update();
    }

    void Update() => CanContinue = Steps.All(s => !s.Def.Required || s.HasImage);

    [RelayCommand]
    async Task Next()
    {
        if (!CanContinue)
        {
            var missing = string.Join(", ", Steps.Where(s => s.Def.Required && !s.HasImage).Select(s => s.Def.Name));
            await Shell.Current.DisplayAlertAsync("Documents required", $"Please scan: {missing}", "OK");
            return;
        }
        await Shell.Current.GoToAsync(nameof(Views.SignaturePage));
    }
}

public partial class DocumentStep(DocumentDef def) : ObservableObject
{
    public DocumentDef Def { get; } = def;
    public byte[]? Image { get; private set; }
    public bool HasImage => Image is not null;
    public bool Skipped { get; set; }

    [ObservableProperty] ImageSource? thumbnail;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(BorderColor))] bool isCurrent;
    [ObservableProperty] string status = def.Required ? "Required" : "Optional";

    public Color BorderColor => IsCurrent ? Color.FromArgb("#512BD4") : Colors.LightGray;

    public void SetImage(byte[] image)
    {
        Image = image;
        Thumbnail = Flow.Image(image);
        Status = "✓ Scanned";
        OnPropertyChanged(nameof(HasImage));
    }
}

// ---------- Step 4: signature + submit ----------
public partial class SignatureViewModel(CheckInSession session, CheckInStorage storage, SyncService sync) : ObservableObject
{
    [ObservableProperty] bool agreed;
    [ObservableProperty] bool isBusy;

    public string AttendeeName => session.FullName;
    public string EventName => session.EventName;
    public string TermsText => session.Event?.TermsText ?? "";

    /// <summary>Called by the page with the exported signature image.</summary>
    public async Task SubmitAsync(byte[]? signature)
    {
        if (signature is null || signature.Length == 0)
        {
            await Shell.Current.DisplayAlertAsync("Signature required", "Please sign in the box before submitting.", "OK");
            return;
        }
        if (!Agreed)
        {
            await Shell.Current.DisplayAlertAsync("Agreement required", "Please accept the terms to continue.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            session.Signature = signature;
            session.LastCompleted = await storage.SaveAsync(session);
            session.Reset();
            Agreed = false;
            _ = sync.SyncNowAsync(); // upload in the background; retried automatically if offline
            await Shell.Current.GoToAsync(nameof(Views.CompletePage));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Save failed", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }
}

// ---------- Done ----------
public partial class CompleteViewModel(CheckInSession session, CheckInStorage storage, EventConfigService events) : ObservableObject
{
    [ObservableProperty] string message = string.Empty;
    [ObservableProperty] ImageSource? photo;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLogo))] ImageSource? logo;
    public bool HasLogo => Logo is not null;
    public bool HasPhoto => Photo is not null;

    public void Load()
    {
        Logo = events.LogoPath is { } lp ? ImageSource.FromFile(lp) : null;
        var r = session.LastCompleted;
        if (r is null) return;
        Message = $"Welcome, {r.FullName}!\nYou're checked in to {r.EventName} at {r.CheckedInAt:t}.";
        var p = storage.GetPath(r, r.PhotoFile);
        Photo = p is null ? null : ImageSource.FromFile(p);
        OnPropertyChanged(nameof(HasPhoto));
    }

    [RelayCommand]
    Task NextAttendee() => Shell.Current.GoToAsync("//home");
}

// ---------- Kiosk's local list ----------
public partial class CheckInListViewModel(CheckInStorage storage) : ObservableObject
{
    public ObservableCollection<CheckInRow> Items { get; } = new();
    [ObservableProperty] string summary = string.Empty;
    public string StorageFolder => storage.RootFolder;

    public async Task LoadAsync()
    {
        Items.Clear();
        foreach (var r in await storage.LoadAllAsync())
            Items.Add(new CheckInRow(r, storage));
        Summary = $"{Items.Count} check-in(s)";
    }
}

public class CheckInRow(CheckInRecord r, CheckInStorage storage)
{
    public string Name => r.FullName;
    public string Details => $"{r.EventName} • {r.CheckedInAt:g} • {r.DocumentFiles.Count} doc(s) • {(r.Synced ? "✅ synced" : "⏳ waiting to sync")}";
    public ImageSource? Photo => storage.GetPath(r, r.PhotoFile) is { } p ? ImageSource.FromFile(p) : null;
    public ImageSource? Signature => storage.GetPath(r, r.SignatureFile) is { } p ? ImageSource.FromFile(p) : null;
}
