using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using System.Diagnostics.CodeAnalysis;
using AndroidX.Core.App;
using AnonWallClient.Background;
using AnonWallClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Android.Util;
using Resource = Microsoft.Maui.Resource;

namespace AnonWallClient.Platforms.Android.Background;

[Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
internal class AndroidForegroundService : Service
{
    // The attribute is now on the constructor
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(AndroidForegroundService))]
    public AndroidForegroundService()
    {
    }

    private static CancellationTokenSource _cts = new();
    private AppLogService? _logger;

    private PowerManager.WakeLock? _wakeLock;
    private bool _isPollingTaskRunning = false;

    public const string ChannelId = "AnonWallClientServiceChannel";
    public const string StopAction = "com.anonwallclient.action.STOP";
    public const string PanicAction = "com.anonwallclient.action.PANIC";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Initialize logger
        _logger = MauiProgram.Services?.GetService<AppLogService>();
        
        if (intent?.Action != null)
        {
            HandleAction(intent.Action);
            return StartCommandResult.NotSticky;
        }

        if (_wakeLock == null)
        {
            if (GetSystemService(PowerService) is PowerManager powerManager)
            {
                _wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, "AnonWallClient:PollingWakeLock");
                _wakeLock.Acquire();
            }
        }

        if (!_isPollingTaskRunning)
        {
            _isPollingTaskRunning = true;
            _cts = new CancellationTokenSource();
            if (MauiProgram.Services is not null)
            {
                var pollingService = MauiProgram.Services.GetService<PollingService>();
                if (pollingService is not null)
                {
                    _ = Task.Run(() => pollingService.StartPollingAsync(_cts.Token));
                }
            }
        }

        CreateNotificationChannel();
        var notification = CreateNotification();
        StartForeground(101, notification);

        return StartCommandResult.Sticky;
    }

    private async void HandleAction(string action)
    {
        if (action == PanicAction)
        {
            try
            {
                _logger?.Add("Android Service: Panic action triggered.");
                
                // Use PanicService if available, otherwise fallback to old method
                var panicService = MauiProgram.Services?.GetService<PanicService>();
                if (panicService != null)
                {
                    var success = await panicService.ExecutePanicAsync();
                    _logger?.Add($"Android Service: Panic execution {(success ? "successful" : "failed")}.");
                }
                else
                {
                    // Fallback to old method
                    _logger?.Add("Android Service: Using fallback panic method.");
                    
                    // Get panic settings from SettingsService first, fallback to Preferences
                    string? panicPath = null;

                    if (MauiProgram.Services is not null)
                    {
                        var settingsService = MauiProgram.Services.GetService<SettingsService>();
                        if (settingsService != null)
                        {
                            panicPath = settingsService.GetPanicFilePath();
                            if (string.IsNullOrEmpty(panicPath))
                                panicPath = settingsService.GetPanicUrl();
                        }
                    }

                    // Fallback to Preferences if SettingsService is not available
                    if (string.IsNullOrEmpty(panicPath))
                    {
                        panicPath = Preferences.Get("panic_file_path", string.Empty);
                        if (string.IsNullOrEmpty(panicPath))
                            panicPath = Preferences.Get("panic_url", string.Empty);
                    }

                    if (!string.IsNullOrEmpty(panicPath) && MauiProgram.Services is not null)
                    {
                        var wallpaperService = MauiProgram.Services.GetService<IWallpaperService>();
                        if (wallpaperService != null)
                        {
                            // Wait for wallpaper to be set before stopping service
                            await wallpaperService.SetWallpaperAsync(panicPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("AnonWallClient", $"Panic action failed: {ex.Message}");
                _logger?.Add($"Android Service: Panic action failed: {ex.Message}");
            }
        }

        // Stop the service after handling the action
        _cts.Cancel();
        StopForeground(true);
        StopSelf();
    }

    private Notification CreateNotification()
    {
        var stopIntent = new Intent(this, typeof(AndroidForegroundService));
        stopIntent.SetAction(StopAction);
        var stopPendingIntent = PendingIntent.GetService(this, 0, stopIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var panicIntent = new Intent(this, typeof(AndroidForegroundService));
        panicIntent.SetAction(PanicAction);
        var panicPendingIntent = PendingIntent.GetService(this, 1, panicIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("AnonWallClient")
            .SetContentText("Actively checking for new wallpapers.")
            .SetSmallIcon(Resource.Mipmap.applogo)
            .SetOngoing(true)
            .AddAction(0, "Panic", panicPendingIntent)
            .AddAction(0, "Stop", stopPendingIntent)
            .Build();
    }

    public override void OnDestroy()
    {
        _cts.Cancel();
        if (_wakeLock?.IsHeld == true)
        {
            _wakeLock.Release();
        }
        _isPollingTaskRunning = false;
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var channel = new NotificationChannel(ChannelId, "AnonWallClient Service", NotificationImportance.Default)
        {
            Description = "Notification channel for the background wallpaper service."
        };
        if (GetSystemService(NotificationService) is NotificationManager notificationManager)
        {
            notificationManager.CreateNotificationChannel(channel);
        }
    }
}
