using CommunityToolkit.Maui;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using AnonWallClient.Background;
using AnonWallClient.Services;
using AnonWallClient.Views;

#if ANDROID
using AnonWallClient.Platforms.Android.Services;
#endif
#if WINDOWS
// We reference the namespace, not a specific class
using AnonWallClient.Platforms.Windows;
#endif

namespace AnonWallClient;

public static class MauiProgram
{
    public static IServiceProvider? Services { get; private set; }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
#if WINDOWS
            .UseNotifyIcon()
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddHttpClient("WalltakerClient", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AnonWallClient/1.0");
        });

        builder.Services.AddSingleton<AppLogService>();
        builder.Services.AddSingleton<WalltakerService>();
        builder.Services.AddSingleton<PollingService>();

#if ANDROID
        builder.Services.AddSingleton<IForegroundServiceManager, ForegroundServiceManager>();
        builder.Services.AddSingleton<IWallpaperService, AnonWallClient.Platforms.Android.WallpaperService>();
#elif WINDOWS
        // For Windows, WallpaperService is in the Platforms.Windows namespace
        builder.Services.AddSingleton<IWallpaperService, WallpaperService>();
#endif

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}