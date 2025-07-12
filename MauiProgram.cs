using Microsoft.Extensions.Logging;
using AnonWallClient.Background;
using AnonWallClient.Services;

#if ANDROID
using AnonWallClient.Platforms.Android.Services;
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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddHttpClient("WalltakerClient");

        builder.Services.AddSingleton<AppLogService>();
        builder.Services.AddSingleton<WalltakerService>();
        builder.Services.AddSingleton<PollingService>();

#if ANDROID
        builder.Services.AddSingleton<IForegroundServiceManager, ForegroundServiceManager>();
        builder.Services.AddSingleton<IWallpaperService, AnonWallClient.Platforms.Android.WallpaperService>();
#elif WINDOWS
        builder.Services.AddSingleton<IWallpaperService, AnonWallClient.Platforms.Windows.WallpaperService>();
#endif

        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}