using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Android.Util;

namespace AnonWallClient.Platforms.Android.Background;

[Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
internal class AndroidForegroundService : Service
{
    public const string ChannelId = "AnonWallClientServiceChannel";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        Log.Info("AnonWallClient", "Simplified Service: OnStartCommand called.");
        CreateNotificationChannel();

        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("AnonWallClient")
            .SetContentText("Background service is running.")
            .SetSmallIcon(global::AnonWallClient.Resource.Mipmap.applogo)
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

        Log.Info("AnonWallClient", "Simplified Service: StartForeground has been called successfully.");
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