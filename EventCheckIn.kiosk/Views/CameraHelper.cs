using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace EventCheckIn.Views;

/// <summary>Shared camera helpers for the photo and document pages.</summary>
public static class CameraHelper
{
    public static async Task<bool> EnsurePermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            await Shell.Current.DisplayAlertAsync("Camera unavailable",
                "Camera permission is required for check-in. Please enable it in settings.", "OK");
        return status == PermissionStatus.Granted;
    }

    public static async Task StartAsync(CameraView camera, CameraPosition preferredPosition)
    {
        if (!await EnsurePermissionAsync()) return;
        try
        {
            await WaitForHandlerAsync(camera);
            var cameras = await camera.GetAvailableCameras(CancellationToken.None);
            var selected = cameras.FirstOrDefault(c => c.Position == preferredPosition) ?? cameras.FirstOrDefault();
            if (selected is not null)
                camera.SelectedCamera = selected;
            await camera.StartCameraPreview(CancellationToken.None);
        }
        catch (Exception ex) { await Shell.Current.DisplayAlertAsync("Camera error", ex.Message, "OK"); }
    }

    /// <summary>
    /// OnAppearing can fire before the CameraView's native handler exists,
    /// so wait for HandlerChanged (max 5 s) before calling camera APIs.
    /// </summary>
    static async Task WaitForHandlerAsync(CameraView camera)
    {
        if (camera.Handler is not null) return;

        var tcs = new TaskCompletionSource();
        void OnHandlerChanged(object? s, EventArgs e)
        {
            if (camera.Handler is not null) tcs.TrySetResult();
        }

        camera.HandlerChanged += OnHandlerChanged;
        try
        {
            if (camera.Handler is null)
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { camera.HandlerChanged -= OnHandlerChanged; }
    }

    public static void Stop(CameraView camera)
    {
        try { camera.StopCameraPreview(); } catch { /* already stopped */ }
    }

    public static async Task<byte[]> ReadAllAsync(Stream s)
    {
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return ms.ToArray();
    }
}
