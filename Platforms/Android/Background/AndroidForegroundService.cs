using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace AnonWallClient.Platforms.Android.Background;

[Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
internal class AndroidForegroundService : Service
{
    public const string ChannelId = "AnonWallClientServiceChannel";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();

        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("AnonWallClient")
            .SetContentText("Actively checking for new wallpapers.")
            .SetSmallIcon(global::AnonWallClient.Resource.Mipmap.applogo)
            .SetOngoing(true)
            .Build();

        // This logic correctly handles different Android versions
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
            // For modern Android, we must provide the service type
            StartForeground(101, notification, ForegroundService.TypeDataSync);
        }
        else
        {
            // For older versions, we call the simpler method
            StartForeground(101, notification);
        }

        return StartCommandResult.Sticky;
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