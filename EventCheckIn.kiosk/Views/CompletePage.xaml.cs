using EventCheckIn.ViewModels;

namespace EventCheckIn.Views;

public partial class CompletePage : ContentPage
{
    readonly CompleteViewModel _vm;
    public CompletePage(CompleteViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
    }

    // Prevent going back into a finished check-in.
    protected override bool OnBackButtonPressed()
    {
        _vm.NextAttendeeCommand.Execute(null);
        return true;
    }
}
