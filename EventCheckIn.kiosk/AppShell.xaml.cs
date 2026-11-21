using EventCheckIn.Views;

namespace EventCheckIn;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(PhotoPage), typeof(PhotoPage));
        Routing.RegisterRoute(nameof(DocumentsPage), typeof(DocumentsPage));
        Routing.RegisterRoute(nameof(SignaturePage), typeof(SignaturePage));
        Routing.RegisterRoute(nameof(CompletePage), typeof(CompletePage));
        Routing.RegisterRoute(nameof(CheckInListPage), typeof(CheckInListPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}
