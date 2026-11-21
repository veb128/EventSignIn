using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class CheckInListPage : ContentPage
{
    readonly CheckInListViewModel _vm;
    public CheckInListPage(CheckInListViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
