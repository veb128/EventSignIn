using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class SignaturePage : ContentPage
{
    readonly SignatureViewModel _vm;
    public SignaturePage(SignatureViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Pad.Lines.Clear();
    }

    void OnClearClicked(object? sender, EventArgs e) => Pad.Lines.Clear();

    async void OnSubmitClicked(object? sender, EventArgs e)
    {
        byte[]? png = null;
        if (Pad.Lines.Count > 0)
        {
            await using var stream = await Pad.GetImageStream(Pad.Width, Pad.Height);
            png = await CameraHelper.ReadAllAsync(stream);
        }
        await _vm.SubmitAsync(png);
    }
}
