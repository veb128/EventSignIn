using CommunityToolkit.Maui;
using EventCheckIn.Services;
using EventCheckIn.ViewModels;
using EventCheckIn.Views;
using Microsoft.Extensions.Logging;

namespace EventCheckIn;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitCamera()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<CheckInSession>();
        builder.Services.AddSingleton<CheckInStorage>();
        builder.Services.AddSingleton<ServerConnection>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddSingleton<EventConfigService>();

        // Pages + view models
        builder.Services.AddTransient<WelcomePage>().AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<PhotoPage>().AddTransient<PhotoViewModel>();
        builder.Services.AddTransient<DocumentsPage>().AddTransient<DocumentsViewModel>();
        builder.Services.AddTransient<SignaturePage>().AddTransient<SignatureViewModel>();
        builder.Services.AddTransient<CompletePage>().AddTransient<CompleteViewModel>();
        builder.Services.AddTransient<CheckInListPage>().AddTransient<CheckInListViewModel>();
        builder.Services.AddTransient<SettingsPage>().AddTransient<SettingsViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
