using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using AnonWallClient.Background;
using AnonWallClient.Services;
using AnonWallClient.Views;

#if ANDROID
using AnonWallClient.Platforms.Android.Services;
#endif
#if WINDOWS
// We reference the namespace, not a specific class
using H.NotifyIcon;
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
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AnonWallClient/1.0.0-alpha.1");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Core services
        builder.Services.AddSingleton<AppLogService>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<ImageCacheService>();
        builder.Services.AddSingleton<WallpaperHistoryService>();
        builder.Services.AddSingleton<WalltakerService>();
        builder.Services.AddSingleton<PollingService>();
        builder.Services.AddSingleton<PanicService>();
        builder.Services.AddSingleton<WallpaperManagementService>();
        builder.Services.AddSingleton<HtmlProfileParserService>();
        builder.Services.AddSingleton<UserProfileService>();

        // Platform-specific services
#if ANDROID
        builder.Services.AddSingleton<IForegroundServiceManager, ForegroundServiceManager>();
        builder.Services.AddSingleton<IWallpaperService, AnonWallClient.Platforms.Android.WallpaperService>();
        builder.Services.AddSingleton<IAutoStartService, AnonWallClient.Platforms.Android.AndroidAutoStartService>();
#elif WINDOWS
        builder.Services.AddSingleton<IWallpaperService, WallpaperService>();
        builder.Services.AddSingleton<IAutoStartService, AnonWallClient.Platforms.Windows.WindowsAutoStartService>();
#elif IOS
        builder.Services.AddSingleton<IWallpaperService, AnonWallClient.Platforms.iOS.WallpaperService>();
        builder.Services.AddSingleton<IAutoStartService, AnonWallClient.Platforms.iOS.iOSAutoStartService>();
#elif MACCATALYST
        builder.Services.AddSingleton<IWallpaperService, AnonWallClient.Platforms.MacCatalyst.WallpaperService>();
        builder.Services.AddSingleton<IAutoStartService, AnonWallClient.Platforms.MacCatalyst.macOSAutoStartService>();
#endif

        // UI services
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<Views.HomePage>();
        builder.Services.AddTransient<Views.HistoryPage>();
        builder.Services.AddTransient<Views.SettingsPage>();
        builder.Services.AddTransient<Views.ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}
