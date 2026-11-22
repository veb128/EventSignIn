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

// ---------- Step 1: attendee details (host-configured form) or Admin check-in list ----------
public partial class WelcomeViewModel(CheckInSession session, SyncService sync, EventConfigService events, ServerConnection server, CheckInStorage storage) : ObservableObject
{
    public SyncService Sync => sync;
    public ObservableCollection<FieldInput> Fields { get; } = new();
    public ObservableCollection<CheckInRow> AdminItems { get; } = new();

    [ObservableProperty] string eventName = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasWelcome))] string welcomeMessage = string.Empty;
    [ObservableProperty] string startButtonText = "Start Check-In →";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLogo))] ImageSource? logo;
    [ObservableProperty] string fullName = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNameError))] string nameError = string.Empty;
    [ObservableProperty] string adminSummary = "0 check-in(s)";
    [ObservableProperty] bool isRefreshingAdmin;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelectedCheckIn))] CheckInRow? selectedCheckIn;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PrimaryActionText))] bool hasStarted;

    // Admin user authentication & PIN reset
    [ObservableProperty] string adminUsername = string.Empty;
    [ObservableProperty] string adminPin = string.Empty;
    [ObservableProperty] string adminNewPin = string.Empty;
    [ObservableProperty] string adminConfirmPin = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsAdminStandardLogin))] bool isAdminPinResetRequired;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAdminAuthError))] string adminAuthError = string.Empty;
    [ObservableProperty] bool isAdminAuthBusy;

    public bool HasLogo => Logo is not null;
    public bool HasWelcome => !string.IsNullOrWhiteSpace(WelcomeMessage);
    public bool HasNameError => !string.IsNullOrEmpty(NameError);
    public bool HasSelectedCheckIn => SelectedCheckIn is not null;
    public bool HasAdminAuthError => !string.IsNullOrEmpty(AdminAuthError);
    public bool IsAdminStandardLogin => !IsAdminPinResetRequired;
    public string PrimaryActionText => HasStarted ? "Next →" : (string.IsNullOrWhiteSpace(StartButtonText) ? "Start Check-In →" : StartButtonText);

    partial void OnStartButtonTextChanged(string value) => OnPropertyChanged(nameof(PrimaryActionText));

    /// <summary>Builds the form from the current event (fresh attendee) or restores it (coming back).</summary>
    public void Load()
    {
        var cfg = session.IsFresh || session.Event is null ? events.Current : session.Event;
        EventName = cfg.EventName;
        WelcomeMessage = cfg.WelcomeMessage;
        StartButtonText = string.IsNullOrWhiteSpace(cfg.StartButtonText) ? "Start Check-In →" : cfg.StartButtonText;
        Logo = events.LogoPath is { } p ? ImageSource.FromFile(p) : null;
        HasStarted = !session.IsFresh;

        FullName = session.IsFresh ? string.Empty : session.FullName;
        NameError = string.Empty;
        foreach (var old in Fields) old.AnswerChanged -= OnFieldAnswerChanged;
        Fields.Clear();
        foreach (var f in cfg.Fields)
        {
            var input = new FieldInput(f, session.IsFresh ? null : session.Answers.FirstOrDefault(a => a.FieldId == f.Id));
            input.AnswerChanged += OnFieldAnswerChanged;
            Fields.Add(input);
        }
        UpdateFieldVisibility();

        if (sync.IsAdminMode && sync.IsAdminAuthenticated && AdminItems.Count == 0 && !IsRefreshingAdmin)
            _ = LoadAdminCheckInsAsync();
    }

    void OnFieldAnswerChanged(object? sender, EventArgs e) => UpdateFieldVisibility();

    bool IsConditionMet(FieldInput f, Dictionary<string, FieldInput> byId, out int depth)
    {
        bool active = true;
        depth = 0;
        var curr = f;
        var visited = new HashSet<string> { curr.Def.Id };
        while (!string.IsNullOrEmpty(curr.Def.ParentFieldId))
        {
            if (!byId.TryGetValue(curr.Def.ParentFieldId, out var parent) || !visited.Add(parent.Def.Id))
            {
                active = false;
                break;
            }
            depth++;
            if (parent.Def.Type == FieldTypes.Text ||
                curr.Def.ShowWhenValues.Count == 0 ||
                !parent.Values.Any(v => curr.Def.ShowWhenValues.Contains(v)))
            {
                active = false;
            }
            curr = parent;
        }
        return active;
    }

    void UpdateFieldVisibility()
    {
        var byId = Fields.Where(f => !string.IsNullOrEmpty(f.Def.Id)).GroupBy(f => f.Def.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var f in Fields)
        {
            bool condMet = IsConditionMet(f, byId, out int depth);
            f.IndentMargin = new Thickness(Math.Min(depth, 4) * 20, 0, 0, 0);
            f.IsActive = condMet && (HasStarted || f.Def.ShowBeforeStart);
        }
    }

    [RelayCommand]
    public async Task LoadAdminCheckInsAsync()
    {
        if (!sync.IsAdminMode || !sync.IsAdminAuthenticated) return;
        IsRefreshingAdmin = true;
        try
        {
            var list = server.IsPaired ? await server.GetCheckInsAsync() : null;
            var rows = new List<CheckInRow>();
            if (list is not null)
            {
                var roleName = list.FirstOrDefault()?.AdminRoleName;
                if (!string.IsNullOrEmpty(roleName)) sync.AdminRoleName = roleName;
                foreach (var r in list)
                {
                    var photoBytes = await server.DownloadCheckInFileAsync(r.Id, r.PhotoFile);
                    var sigBytes = await server.DownloadCheckInFileAsync(r.Id, r.SignatureFile);
                    var qrBytes = await server.DownloadCheckInFileAsync(r.Id, r.QrCodeFile ?? "qrcode.png")
                                  ?? QrCodeHelper.GeneratePng(r.Id);
                    var docs = new List<DocumentPreview>();
                    for (int i = 0; i < r.DocumentFiles.Count; i++)
                    {
                        var docFile = r.DocumentFiles[i];
                        var docName = r.DocumentNames.ElementAtOrDefault(i) ?? $"Document {i + 1}";
                        var access = r.DocumentAccess.ElementAtOrDefault(i) ?? "allowed";
                        ImageSource? img = null;
                        if (access == "allowed")
                        {
                            var docBytes = await server.DownloadCheckInFileAsync(r.Id, docFile);
                            img = Flow.Image(docBytes);
                        }
                        docs.Add(new DocumentPreview(r.Id, docFile, docName, access, img));
                    }
                    rows.Add(new CheckInRow(r, photoBytes, sigBytes, qrBytes, docs));
                }
            }
            else
            {
                foreach (var r in await storage.LoadAllAsync())
                    rows.Add(new CheckInRow(r, storage));
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var prevId = SelectedCheckIn?.Id;
                AdminItems.Clear();
                foreach (var row in rows) AdminItems.Add(row);
                AdminSummary = $"{AdminItems.Count} attendee(s) checked in";
                if (prevId is not null)
                    SelectedCheckIn = AdminItems.FirstOrDefault(x => x.Id == prevId);
            });
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsRefreshingAdmin = false);
        }
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
        UpdateFieldVisibility();
        bool ok = true;
        if (string.IsNullOrWhiteSpace(FullName)) { NameError = "Please enter your full name."; ok = false; }
        foreach (var f in Fields.Where(f => f.IsActive)) ok &= f.Validate();
        if (!ok) return;

        if (!HasStarted)
        {
            var byId = Fields.Where(f => !string.IsNullOrEmpty(f.Def.Id)).GroupBy(f => f.Def.Id).ToDictionary(g => g.Key, g => g.First());
            bool hasAfterStartFields = Fields.Any(f => !f.Def.ShowBeforeStart &&
                (IsConditionMet(f, byId, out _) ||
                 (!string.IsNullOrEmpty(f.Def.ParentFieldId) && byId.TryGetValue(f.Def.ParentFieldId, out var p) && !p.Def.ShowBeforeStart)));
            if (hasAfterStartFields)
            {
                HasStarted = true;
                UpdateFieldVisibility();
                return;
            }
        }

        var map = Fields.Where(f => !string.IsNullOrEmpty(f.Def.Id)).GroupBy(f => f.Def.Id).ToDictionary(g => g.Key, g => g.First());
        session.Event = session.IsFresh || session.Event is null ? events.Current : session.Event;
        session.FullName = FullName.Trim();
        session.Answers = Fields.Where(f => IsConditionMet(f, map, out _)).Select(f => f.ToAnswer()).ToList();
        session.IsFresh = false;
        await Flow.AfterDetails(session);
    }

    [RelayCommand]
    Task OpenSettings() => Shell.Current.GoToAsync(nameof(Views.SettingsPage));

    [RelayCommand]
    async Task StartInCheckInMode()
    {
        IsAdminAuthBusy = true;
        try
        {
            await server.SelectKioskModeAsync(isAdmin: false);
            sync.IsAdminMode = false;
            sync.IsModeLocked = true;
            sync.HasChosenStartupMode = true;
        }
        finally
        {
            IsAdminAuthBusy = false;
        }
    }

    [RelayCommand]
    async Task StartInAdminMode()
    {
        AdminAuthError = string.Empty;
        IsAdminPinResetRequired = false;
        server.IsAdminMode = true;
        sync.IsAdminMode = true;
        sync.HasChosenStartupMode = true;
        await server.SelectKioskModeAsync(isAdmin: true);
    }

    [RelayCommand]
    void BackToModePrompt()
    {
        AdminAuthError = string.Empty;
        IsAdminPinResetRequired = false;
        sync.HasChosenStartupMode = false;
    }

    [RelayCommand]
    async Task AdminLogin()
    {
        AdminAuthError = string.Empty;
        if (string.IsNullOrWhiteSpace(AdminUsername))
        {
            AdminAuthError = "Please enter your admin username.";
            return;
        }
        IsAdminAuthBusy = true;
        try
        {
            var res = await server.AdminLoginAsync(AdminUsername.Trim(), AdminPin.Trim());
            if (!res.Ok)
            {
                AdminAuthError = res.Error ?? "Authentication failed.";
                return;
            }
            if (res.MustResetPin)
            {
                IsAdminPinResetRequired = true;
                AdminNewPin = string.Empty;
                AdminConfirmPin = string.Empty;
                AdminAuthError = "A PIN reset is required for your account. Enter a new PIN below.";
                return;
            }
            AdminPin = string.Empty;
            IsAdminPinResetRequired = false;
            sync.IsAdminAuthenticated = true;
            sync.AdminUserName = res.DisplayName;
            sync.AdminRoleName = res.RoleName;
            await LoadAdminCheckInsAsync();
        }
        finally
        {
            IsAdminAuthBusy = false;
        }
    }

    [RelayCommand]
    async Task AdminResetPin()
    {
        AdminAuthError = string.Empty;
        if (string.IsNullOrWhiteSpace(AdminNewPin) || AdminNewPin.Trim().Length < 4)
        {
            AdminAuthError = "New PIN must be at least 4 characters.";
            return;
        }
        if (AdminNewPin.Trim() != AdminConfirmPin.Trim())
        {
            AdminAuthError = "New PIN and confirmation do not match.";
            return;
        }
        IsAdminAuthBusy = true;
        try
        {
            var res = await server.AdminResetPinAsync(AdminUsername.Trim(), AdminPin.Trim(), AdminNewPin.Trim());
            if (!res.Ok)
            {
                AdminAuthError = res.Error ?? "Could not reset PIN.";
                return;
            }
            AdminPin = string.Empty;
            AdminNewPin = string.Empty;
            AdminConfirmPin = string.Empty;
            IsAdminPinResetRequired = false;
            sync.IsAdminAuthenticated = true;
            sync.AdminUserName = res.DisplayName;
            sync.AdminRoleName = res.RoleName;
            await LoadAdminCheckInsAsync();
        }
        finally
        {
            IsAdminAuthBusy = false;
        }
    }

    [RelayCommand]
    void CancelAdminPinReset()
    {
        IsAdminPinResetRequired = false;
        AdminNewPin = string.Empty;
        AdminConfirmPin = string.Empty;
        AdminAuthError = string.Empty;
    }

    [RelayCommand]
    async Task AdminLogout()
    {
        SelectedCheckIn?.LockAllPinProtectedDocuments();
        SelectedCheckIn = null;
        AdminItems.Clear();
        AdminPin = string.Empty;
        IsAdminPinResetRequired = false;
        AdminAuthError = string.Empty;
        await server.AdminLogoutAsync();
        sync.IsAdminAuthenticated = false;
        sync.AdminUserName = string.Empty;
        sync.AdminRoleName = string.Empty;
        sync.HasChosenStartupMode = false;
    }

    [RelayCommand]
    void SelectCheckIn(CheckInRow? row)
    {
        SelectedCheckIn?.LockAllPinProtectedDocuments();
        SelectedCheckIn = row;
    }

    [RelayCommand]
    void ClearSelectedCheckIn()
    {
        SelectedCheckIn?.LockAllPinProtectedDocuments();
        SelectedCheckIn = null;
    }

    [RelayCommand]
    async Task UnlockDocument(DocumentPreview? doc)
    {
        if (doc is null || !doc.IsLocked) return;
        var pin = await Shell.Current.DisplayPromptAsync(
            "PIN Required",
            $"Enter the PIN/password for your admin role to view \"{doc.Name}\":",
            accept: "Unlock",
            cancel: "Cancel",
            placeholder: "PIN / password",
            maxLength: 40,
            keyboard: Keyboard.Text);
        if (string.IsNullOrWhiteSpace(pin)) return;

        doc.IsUnlocking = true;
        try
        {
            var (bytes, error) = await server.DownloadProtectedDocumentAsync(doc.CheckInId, doc.FileName, pin.Trim());
            if (bytes is not null)
            {
                doc.SetUnlockedImage(Flow.Image(bytes));
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Access denied", error ?? "Incorrect PIN.", "OK");
            }
        }
        finally
        {
            doc.IsUnlocking = false;
        }
    }

    [RelayCommand]
    void LockDocument(DocumentPreview? doc) => doc?.Lock();

    [RelayCommand]
    Task ScanQrCode() => Shell.Current.GoToAsync(nameof(Views.QrScannerPage));

    public async Task<bool> ShowAttendeeByIdAsync(string checkInId)
    {
        var match = AdminItems.FirstOrDefault(x => string.Equals(x.Id, checkInId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            await LoadAdminCheckInsAsync();
            match = AdminItems.FirstOrDefault(x => string.Equals(x.Id, checkInId, StringComparison.OrdinalIgnoreCase));
        }
        if (match is not null)
        {
            SelectedCheckIn?.LockAllPinProtectedDocuments();
            SelectedCheckIn = match;
            return true;
        }
        return false;
    }
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
    [ObservableProperty] ImageSource? qrCode;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLogo))] ImageSource? logo;
    public bool HasLogo => Logo is not null;
    public bool HasPhoto => Photo is not null;
    public bool HasQrCode => QrCode is not null;

    public void Load()
    {
        Logo = events.LogoPath is { } lp ? ImageSource.FromFile(lp) : null;
        var r = session.LastCompleted;
        if (r is null) return;
        Message = $"Welcome, {r.FullName}!\nYou're checked in to {r.EventName} at {r.CheckedInAt:t}.";
        var p = storage.GetPath(r, r.PhotoFile);
        Photo = p is null ? null : ImageSource.FromFile(p);
        var q = storage.GetPath(r, r.QrCodeFile ?? "qrcode.png");
        QrCode = q is not null && File.Exists(q) ? ImageSource.FromFile(q) : Flow.Image(QrCodeHelper.GeneratePng(r.Id));
        OnPropertyChanged(nameof(HasPhoto));
        OnPropertyChanged(nameof(HasQrCode));
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

public partial class DocumentPreview : ObservableObject
{
    public string CheckInId { get; }
    public string FileName { get; }
    public string Name { get; }
    public string Access { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsUnlocked), nameof(IsLocked), nameof(CanRelock), nameof(StatusText))]
    ImageSource? image;

    [ObservableProperty] bool isUnlocking;

    public DocumentPreview(string checkInId, string fileName, string name, string access, ImageSource? image)
    {
        CheckInId = checkInId;
        FileName = fileName;
        Name = name;
        Access = access;
        Image = image;
    }

    public bool IsRestricted => Access == "restricted";
    public bool RequiresPin => Access == "pin_required";
    public bool IsLocked => RequiresPin && Image is null;
    public bool IsUnlocked => !IsRestricted && Image is not null;
    public bool CanRelock => RequiresPin && Image is not null;

    public string StatusText => IsRestricted ? "✓ Scanned (Restricted)"
                              : IsLocked ? "✓ Scanned — PIN required"
                              : "✓ Scanned";

    public void SetUnlockedImage(ImageSource? img) => Image = img;

    public void Lock()
    {
        if (RequiresPin) Image = null;
    }
}

public class CheckInRow
{
    readonly CheckInRecord _r;
    public CheckInRow(CheckInRecord r, CheckInStorage storage)
    {
        _r = r;
        Photo = storage.GetPath(r, r.PhotoFile) is { } p && File.Exists(p) ? ImageSource.FromFile(p) : null;
        Signature = storage.GetPath(r, r.SignatureFile) is { } s && File.Exists(s) ? ImageSource.FromFile(s) : null;
        var q = storage.GetPath(r, r.QrCodeFile ?? "qrcode.png");
        QrCode = q is not null && File.Exists(q) ? ImageSource.FromFile(q) : Flow.Image(QrCodeHelper.GeneratePng(r.Id));
        var docs = new List<DocumentPreview>();
        for (int i = 0; i < r.DocumentFiles.Count; i++)
        {
            var dp = storage.GetPath(r, r.DocumentFiles[i]);
            var dn = r.DocumentNames.ElementAtOrDefault(i) ?? $"Document {i + 1}";
            if (dp is not null && File.Exists(dp))
                docs.Add(new DocumentPreview(r.Id, r.DocumentFiles[i], dn, "allowed", ImageSource.FromFile(dp)));
        }
        Documents = docs;
    }

    public CheckInRow(CheckInRecord r, byte[]? photoBytes, byte[]? sigBytes, byte[]? qrBytes = null, List<DocumentPreview>? docs = null)
    {
        _r = r;
        Photo = Flow.Image(photoBytes);
        Signature = Flow.Image(sigBytes);
        QrCode = Flow.Image(qrBytes ?? QrCodeHelper.GeneratePng(r.Id));
        Documents = docs ?? new();
    }

    public void LockAllPinProtectedDocuments()
    {
        foreach (var d in Documents) d.Lock();
    }

    public string Id => _r.Id;
    public string Name => _r.FullName;
    public string EventName => _r.EventName;
    public string CheckedInTime => _r.CheckedInAt.ToString("f");
    public string KioskDisplay => string.IsNullOrWhiteSpace(_r.KioskName) ? "Local kiosk" : _r.KioskName;
    public string Details
    {
        get
        {
            var parts = new List<string> { _r.EventName, _r.CheckedInAt.ToString("g") };
            if (!string.IsNullOrWhiteSpace(_r.KioskName)) parts.Add(_r.KioskName);
            parts.Add($"{_r.DocumentFiles.Count} doc(s)");
            return string.Join(" • ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }
    public string AnswersSummary =>
        _r.Answers is { Count: > 0 }
            ? string.Join("  |  ", _r.Answers.Where(a => a.Values.Count > 0).Select(a => $"{a.Label}: {string.Join(", ", a.Values)}"))
            : _r.Email;
    public string AnswersMultiline =>
        _r.Answers is { Count: > 0 }
            ? string.Join("\n", _r.Answers.Where(a => a.Values.Count > 0).Select(a => $"• {a.Label}: {string.Join(", ", a.Values)}"))
            : (!string.IsNullOrWhiteSpace(_r.Email) ? $"• Email: {_r.Email}" : "No additional responses.");
    public bool HasAnswers => !string.IsNullOrWhiteSpace(AnswersSummary);
    public bool HasPhoto => Photo is not null;
    public bool HasSignature => Signature is not null;
    public bool HasDocuments => Documents.Count > 0;
    public ImageSource? Photo { get; }
    public ImageSource? Signature { get; }
    public ImageSource? QrCode { get; }
    public List<DocumentPreview> Documents { get; }
}
