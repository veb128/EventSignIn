using EventCheckIn.Services;
using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class WelcomePage : ContentPage
{
    readonly WelcomeViewModel _vm;
    readonly EventConfigService _events;

    public WelcomePage(WelcomeViewModel vm, EventConfigService events)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _events = events;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
        _events.Changed += OnEventChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _events.Changed -= OnEventChanged;
    }

    void OnEventChanged(object? sender, EventArgs e) => _vm.OnEventChanged();

    // Tapping a checkbox's text toggles it too (bigger touch target on kiosks).
    void OnOptionLabelTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is OptionItem o) o.IsSelected = !o.IsSelected;
    }
}
