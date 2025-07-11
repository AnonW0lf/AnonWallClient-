using Microsoft.Extensions.Logging;
using AnonWallClient.Background;
using AnonWallClient.Services;

#if ANDROID
using AnonWallClient.Platforms.Android;
#elif WINDOWS
using AnonWallClient.Platforms.Windows;
#endif

namespace AnonWallClient;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services for dependency injection
        builder.Services.AddHttpClient("WalltakerClient");
        builder.Services.AddSingleton<AppLogService>();
        builder.Services.AddSingleton<WalltakerService>();

        // Register platform-specific wallpaper services
#if ANDROID
        builder.Services.AddSingleton<IWallpaperService>(provider =>
            new AnonWallClient.Platforms.Android.WallpaperService(
                new HttpClient(),
                provider.GetRequiredService<AppLogService>()
            )
        );
        builder.Services.AddSingleton<IForegroundServiceManager, AnonWallClient.Platforms.Android.Services.ForegroundServiceManager>();
#elif WINDOWS
        builder.Services.AddSingleton<IWallpaperService>(provider =>
            new AnonWallClient.Platforms.Windows.WallpaperService(
                new HttpClient(),
                provider.GetRequiredService<AppLogService>()
            )
        );
#endif

        // Register the simplified PollingService (with no dependencies for now)
        builder.Services.AddSingleton<PollingService>();

        // Register the MainPage so it can be injected into App.xaml.cs
        builder.Services.AddSingleton<MainPage>();


#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}