using CommunityToolkit.Mvvm.ComponentModel;

namespace EventCheckIn.Services;

/// <summary>
/// Background sync: every check-in is saved on the kiosk first, then uploaded to the
/// host whenever it's reachable. Works offline; retries automatically.
/// </summary>
public partial class SyncService(ServerConnection server, CheckInStorage storage, EventConfigService events) : ObservableObject
{
    static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);
    readonly SemaphoreSlim _lock = new(1, 1);
    CancellationTokenSource? _loop;

    [ObservableProperty] int pendingCount;
    [ObservableProperty] bool isOnline;
    [ObservableProperty] string statusText = "Not connected to a host";
    [ObservableProperty] bool showSettings = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsKioskMode), nameof(IsAdminModeVisible), nameof(IsModePromptVisible))] bool isAdminMode;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsKioskMode), nameof(IsAdminModeVisible), nameof(IsModePromptVisible))] bool isModeLocked;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsKioskMode), nameof(IsAdminModeVisible), nameof(IsModePromptVisible))] bool hasChosenStartupMode;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsAdminLoginVisible))] bool isAdminAuthenticated;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AdminHeaderText))] string adminUserName = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AdminHeaderText))] string adminRoleName = string.Empty;

    /// <summary>True when the tablet should prompt the user at startup to choose Check-In Kiosk vs Admin Mode.</summary>
    public bool IsModePromptVisible => !IsModeLocked && !HasChosenStartupMode;
    public bool IsKioskMode => !IsModePromptVisible && !IsAdminMode;
    public bool IsAdminModeVisible => !IsModePromptVisible && IsAdminMode;
    public bool IsAdminLoginVisible => !IsAdminAuthenticated;
    public string AdminHeaderText => !string.IsNullOrWhiteSpace(AdminUserName) && !string.IsNullOrWhiteSpace(AdminRoleName)
        ? $"{AdminUserName} ({AdminRoleName}) — Checked-In Attendees"
        : !string.IsNullOrWhiteSpace(AdminRoleName)
            ? $"Admin View ({AdminRoleName}) — Checked-In Attendees"
            : "Admin View — Checked-In Attendees";
    public event EventHandler? Synced;

    public void Start()
    {
        if (_loop is not null) return;
        ShowSettings = !server.IsPaired;
        IsAdminMode = server.IsAdminMode;
        IsModeLocked = server.IsModeLocked;
        HasChosenStartupMode = server.IsModeLocked; // If locked in non-admin mode, skip startup prompt; otherwise prompt on launch.
        _loop = new CancellationTokenSource();
        var ct = _loop.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await SyncNowAsync(ct);
                try { await Task.Delay(Interval, ct); } catch (TaskCanceledException) { }
            }
        }, ct);
    }

    /// <summary>Uploads all pending check-ins. Safe to call any time (e.g. right after a check-in).</summary>
    public async Task SyncNowAsync(CancellationToken ct = default)
    {
        if (!await _lock.WaitAsync(0, ct)) return; // a sync is already running
        try
        {
            var pending = (await storage.LoadAllAsync()).Where(r => !r.Synced).OrderBy(r => r.CheckedInAt).ToList();
            Update(pending.Count, IsOnline);

            if (!server.IsPaired) { Update(pending.Count, false, "Not paired with a host – check-ins are saved on this device", showSettings: true, isAdmin: server.IsAdminMode, modeLocked: server.IsModeLocked); return; }

            var ping = await server.PingAsync(ct);
            if (ping.Unauthorized)
            {
                Update(pending.Count, false, "Host no longer recognises this kiosk – pair again in Kiosk settings", showSettings: true, isAdmin: server.IsAdminMode, modeLocked: server.IsModeLocked);
                return;
            }
            if (!ping.Online) { Update(pending.Count, false, $"Host offline – {pending.Count} check-in(s) waiting", showSettings: false, isAdmin: server.IsAdminMode, modeLocked: server.IsModeLocked); return; }

            // Pick up any event changes made by the admin on the host.
            await events.RefreshAsync(server, ct);

            int left = pending.Count;
            foreach (var rec in pending)
            {
                var result = await server.UploadAsync(rec, storage, ct);
                if (result == ServerConnection.UploadResult.Ok)
                {
                    rec.Synced = true;
                    rec.SyncedAt = DateTime.Now;
                    await storage.UpdateAsync(rec);
                    left--;
                }
                else if (result == ServerConnection.UploadResult.Unauthorized)
                {
                    Update(left, false, "Host no longer recognises this kiosk – pair again in Kiosk settings", showSettings: true, isAdmin: ping.IsAdmin, modeLocked: ping.ModeLocked);
                    return;
                }
                else if (result == ServerConnection.UploadResult.Offline) break;
                // Rejected: skip this record for now, keep it locally.
            }
            Update(left, true, left == 0 ? $"Connected to {server.ServerName} – all synced" : $"Connected – {left} check-in(s) waiting", showSettings: ping.ShowSettings, isAdmin: ping.IsAdmin, modeLocked: ping.ModeLocked, adminAuth: ping.AdminAuthenticated, userName: ping.AdminUserName, roleName: ping.AdminRoleName);
        }
        catch (Exception ex) { Update(PendingCount, false, $"Sync error: {ex.Message}"); }
        finally { _lock.Release(); }
    }

    void Update(int pending, bool online, string? text = null, bool? showSettings = null, bool? isAdmin = null, bool? modeLocked = null, bool? adminAuth = null, string? userName = null, string? roleName = null) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PendingCount = pending;
            IsOnline = online;
            if (text is not null) StatusText = text;
            if (showSettings is not null) ShowSettings = showSettings.Value;
            if (modeLocked is not null)
            {
                bool wasLocked = IsModeLocked;
                IsModeLocked = modeLocked.Value;
                // If the host unlocked a previously locked check-in kiosk (set to "Prompt on Tablet") and didn't set it directly to Admin, show the prompt.
                if (wasLocked && !modeLocked.Value && isAdmin != true)
                    HasChosenStartupMode = false;
                else if (modeLocked.Value)
                    HasChosenStartupMode = true;
            }
            if (isAdmin is not null)
            {
                bool wasAdmin = IsAdminMode;
                if (isAdmin.Value)
                {
                    IsAdminMode = true;
                    if (!wasAdmin) HasChosenStartupMode = true;
                }
                else if (IsModeLocked)
                {
                    // Only force out of Admin mode when the host has locked the tablet into Check-In mode.
                    IsAdminMode = false;
                }
            }
            if (adminAuth is not null) IsAdminAuthenticated = adminAuth.Value;
            if (userName is not null) AdminUserName = userName;
            if (roleName is not null) AdminRoleName = roleName;
            Synced?.Invoke(this, EventArgs.Empty);
        });
}
