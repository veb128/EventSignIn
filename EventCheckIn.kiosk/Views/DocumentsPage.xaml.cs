using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class DocumentsPage : ContentPage
{
    readonly DocumentsViewModel _vm;
    public DocumentsPage(DocumentsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
        await CameraHelper.StartAsync(Camera);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraHelper.Stop(Camera);
    }

    async void OnScanClicked(object? sender, EventArgs e)
    {
        ScanButton.IsEnabled = false;
        await Camera.CaptureImage(CancellationToken.None);
    }

    async void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
    {
        var bytes = await CameraHelper.ReadAllAsync(e.Media);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _vm.Capture(bytes);
            ScanButton.IsEnabled = true;
        });
    }
}
