using AnonWallClient.Background;
using AnonWallClient.Services;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;


#if ANDROID
using AnonWallClient.Platforms.Android.Services;
#endif
#if WINDOWS
using Windows.Media.Protection.PlayReady;
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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        //builder.Services.AddHttpClient("WalltakerClient");
        // Configure the HttpClient to add a unique User-Agent to all requests
        builder.Services.AddHttpClient("WalltakerClient", client =>
               {
                        // Format: AppName/Version
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AnonWallClient/1.0");
                    });

        builder.Services.AddSingleton<AppLogService>();
        builder.Services.AddSingleton<WalltakerService>();
        builder.Services.AddSingleton<PollingService>();

#if ANDROID
        builder.Services.AddSingleton<IForegroundServiceManager, ForegroundServiceManager>();
        builder.Services.AddSingleton<IWallpaperService, AnonWallClient.Platforms.Android.WallpaperService>();
#elif WINDOWS
        //builder.Services.AddSingleton<IWallpaperService, AnonWallClient.Platforms.Windows.WallpaperService>();
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