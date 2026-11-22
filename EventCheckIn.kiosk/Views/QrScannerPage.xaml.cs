using CommunityToolkit.Maui.Core;
using EventCheckIn.Services;
using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class QrScannerPage : ContentPage
{
    readonly WelcomeViewModel _welcomeVm;
    CameraPosition _cameraPosition = CameraPosition.Rear;
    CancellationTokenSource? _scanLoop;
    bool _isCapturing;
    bool _found;

    public QrScannerPage(WelcomeViewModel welcomeVm)
    {
        InitializeComponent();
        _welcomeVm = welcomeVm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _found = false;
        await CameraHelper.StartAsync(Camera, _cameraPosition);
        StartAutoScan();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAutoScan();
        CameraHelper.Stop(Camera);
    }

    void StartAutoScan()
    {
        StopAutoScan();
        _scanLoop = new CancellationTokenSource();
        var ct = _scanLoop.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(900, ct);
                while (!ct.IsCancellationRequested && !_found)
                {
                    if (!_isCapturing)
                    {
                        _isCapturing = true;
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            try { await Camera.CaptureImage(ct); }
                            catch { _isCapturing = false; }
                        });
                    }
                    await Task.Delay(1200, ct);
                }
            }
            catch (TaskCanceledException) { }
        }, ct);
    }

    void StopAutoScan()
    {
        try { _scanLoop?.Cancel(); } catch { }
        _scanLoop = null;
    }

    async void OnScanNowClicked(object? sender, EventArgs e)
    {
        if (_isCapturing || _found) return;
        _isCapturing = true;
        StatusLabel.Text = "Scanning…";
        try { await Camera.CaptureImage(CancellationToken.None); }
        catch { _isCapturing = false; }
    }

    async void OnSwitchCameraClicked(object? sender, EventArgs e)
    {
        _cameraPosition = _cameraPosition == CameraPosition.Rear ? CameraPosition.Front : CameraPosition.Rear;
        CameraHelper.Stop(Camera);
        await CameraHelper.StartAsync(Camera, _cameraPosition);
    }

    async void OnCancelClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");

    async void OnManualLookupClicked(object? sender, EventArgs e)
    {
        var id = QrCodeHelper.TryExtractCheckInId(ManualIdEntry.Text);
        if (id is null)
        {
            StatusLabel.Text = "Invalid QR/check-in code format.";
            return;
        }
        await HandleDecodedIdAsync(id);
    }

    async void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
    {
        try
        {
            if (_found) return;
            var bytes = await CameraHelper.ReadAllAsync(e.Media);
            var rawText = await Task.Run(() => QrCodeHelper.TryDecodeQrFromImage(bytes));
            var id = QrCodeHelper.TryExtractCheckInId(rawText);
            if (id is not null)
            {
                await MainThread.InvokeOnMainThreadAsync(() => HandleDecodedIdAsync(id));
            }
        }
        finally
        {
            _isCapturing = false;
        }
    }

    async Task HandleDecodedIdAsync(string id)
    {
        if (_found) return;
        _found = true;
        StopAutoScan();
        StatusLabel.Text = "QR Code recognised! Loading attendee info…";

        bool foundAttendee = await _welcomeVm.ShowAttendeeByIdAsync(id);
        if (foundAttendee)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            _found = false;
            StatusLabel.Text = $"No attendee found for QR code ({id[..8]}…). Point at another QR code.";
            StartAutoScan();
        }
    }
}
