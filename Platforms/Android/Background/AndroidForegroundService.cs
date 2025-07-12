using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AnonWallClient.Background;
using Android.Util;

namespace AnonWallClient.Platforms.Android.Background;

[Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
internal class AndroidForegroundService : Service
{
    private PowerManager.WakeLock? _wakeLock;
    private bool _isPollingTaskRunning = false;

    public const string ChannelId = "AnonWallClientServiceChannel";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (_wakeLock == null)
        {
            var powerManager = (PowerManager)GetSystemService(PowerService);
            _wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, "AnonWallClient:PollingWakeLock");
            _wakeLock.Acquire();
        }

        if (!_isPollingTaskRunning)
        {
            _isPollingTaskRunning = true;
            var pollingService = MauiProgram.Services!.GetService<PollingService>();
            _ = Task.Run(() => pollingService!.StartPollingAsync(new CancellationToken()));
        }

        CreateNotificationChannel();

        // THE FIX: Find the icon resource ID dynamically at runtime
        int iconId = Resources.GetIdentifier("applogo", "mipmap", PackageName);
        if (iconId == 0)
        {
            // Fallback if the icon isn't found, though it should be.
            Log.Error("AnonWallClient", "Failed to find notification icon 'applogo'. Using default.");
            iconId = ApplicationInfo.Icon;
        }

        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("AnonWallClient")
            .SetContentText("Actively checking for new wallpapers.")
            .SetSmallIcon(iconId) // Use the dynamically found ID
            .SetOngoing(true)
            .Build();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            StartForeground(101, notification, ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(101, notification);
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        if (_wakeLock?.IsHeld == true)
        {
            _wakeLock.Release();
        }
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