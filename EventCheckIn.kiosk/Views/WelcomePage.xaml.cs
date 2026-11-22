using EventCheckIn.Services;
using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class WelcomePage : ContentPage
{
    readonly WelcomeViewModel _vm;
    readonly EventConfigService _events;
    readonly SyncService _sync;

    public WelcomePage(WelcomeViewModel vm, EventConfigService events, SyncService sync)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _events = events;
        _sync = sync;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
        _events.Changed += OnEventChanged;
        _sync.Synced += OnSynced;
        Dispatcher.Dispatch(async () =>
        {
            UpdateScrollIndicator();
            if (_vm.HasSelectedCheckIn)
                await PageScroll.ScrollToAsync(AttendeeDetailCard, ScrollToPosition.Start, true);
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _events.Changed -= OnEventChanged;
        _sync.Synced -= OnSynced;
    }

    async void OnSynced(object? sender, EventArgs e)
    {
        if (_sync.IsAdminMode)
        {
            if (_sync.IsAdminAuthenticated) await _vm.LoadAdminCheckInsAsync();
            else
            {
                _vm.SelectedCheckIn?.LockAllPinProtectedDocuments();
                _vm.SelectedCheckIn = null;
                _vm.AdminItems.Clear();
            }
        }
        Dispatcher.Dispatch(UpdateScrollIndicator);
    }

    void OnEventChanged(object? sender, EventArgs e)
    {
        _vm.OnEventChanged();
        Dispatcher.Dispatch(UpdateScrollIndicator);
    }

    // Tapping a checkbox's text toggles it too (bigger touch target on kiosks).
    void OnOptionLabelTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is OptionItem o) o.IsSelected = !o.IsSelected;
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
