using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class SettingsPage : ContentPage
{
    readonly SettingsViewModel _vm;
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
        _vm.HostSelected += OnHostSelected;
        Dispatcher.Dispatch(UpdateScrollIndicator);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.HostSelected -= OnHostSelected;
    }

    async void OnHostSelected(object? sender, EventArgs e)
    {
        await Task.Delay(100); // allow layout to expand with PairSection
        UpdateScrollIndicator();
        await PageScroll.ScrollToAsync(PairSection, ScrollToPosition.MakeVisible, true);
    }

    void OnPageScrolled(object? sender, ScrolledEventArgs e) => UpdateScrollIndicator();

    void OnScrollSizeChanged(object? sender, EventArgs e) => UpdateScrollIndicator();

    async void OnScrollMoreTapped(object? sender, TappedEventArgs e)
    {
        var targetY = Math.Min(PageScroll.ScrollY + PageScroll.Height * 0.75,
                               Math.Max(0, PageContent.Height - PageScroll.Height));
        await PageScroll.ScrollToAsync(0, targetY, true);
    }

    void UpdateScrollIndicator()
    {
        if (PageScroll.Height <= 0 || PageContent.Height <= 0) return;
        double remaining = PageContent.Height - (PageScroll.ScrollY + PageScroll.Height);
        ScrollMoreIndicator.IsVisible = remaining > 24;
    }
}
