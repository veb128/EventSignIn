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

    public void Start()
    {
        if (_loop is not null) return;
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

            if (!server.IsPaired) { Update(pending.Count, false, "Not paired with a host – check-ins are saved on this device"); return; }

            bool online = await server.PingAsync(ct);
            if (!online) { Update(pending.Count, false, $"Host offline – {pending.Count} check-in(s) waiting"); return; }

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
                    Update(left, false, "Host no longer recognises this kiosk – pair again in Kiosk settings");
                    return;
                }
                else if (result == ServerConnection.UploadResult.Offline) break;
                // Rejected: skip this record for now, keep it locally.
            }
            Update(left, true, left == 0 ? $"Connected to {server.ServerName} – all synced" : $"Connected – {left} check-in(s) waiting");
        }
        catch (Exception ex) { Update(PendingCount, false, $"Sync error: {ex.Message}"); }
        finally { _lock.Release(); }
    }

    void Update(int pending, bool online, string? text = null) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PendingCount = pending;
            IsOnline = online;
            if (text is not null) StatusText = text;
        });
}
