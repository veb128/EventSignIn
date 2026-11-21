using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class PhotoPage : ContentPage
{
    readonly PhotoViewModel _vm;
    public PhotoPage(PhotoViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
        if (_vm.IsCapturing) await CameraHelper.StartAsync(Camera);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraHelper.Stop(Camera);
    }

    async void OnCaptureClicked(object? sender, EventArgs e) =>
        await Camera.CaptureImage(CancellationToken.None);

    async void OnRetakeClicked(object? sender, EventArgs e) => await CameraHelper.StartAsync(Camera);

    async void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
    {
        var bytes = await CameraHelper.ReadAllAsync(e.Media);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _vm.SetPhoto(bytes);
            CameraHelper.Stop(Camera);
        });
    }
}
