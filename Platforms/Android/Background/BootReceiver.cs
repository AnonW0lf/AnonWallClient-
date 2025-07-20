using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using System.Diagnostics.CodeAnalysis;

namespace AnonWallClient.Platforms.Android.Background;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    // The attribute is now on the constructor
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(BootReceiver))]
    public BootReceiver()
    {
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context != null && intent?.Action == Intent.ActionBootCompleted)
        {
            try
            {
                // Check if user has enabled auto-start after boot
                var autoStartEnabled = Preferences.Get("AutoStartEnabled", false);
                if (!autoStartEnabled)
                {
                    Log.Info("AnonWallClient", "BootReceiver: AutoStart is disabled in settings.");
                    return;
                }

                // Only start service if user has configured the app with valid settings
                var linkId = Preferences.Get("LinkId", string.Empty);
                if (!string.IsNullOrEmpty(linkId))
                {
                    var serviceIntent = new Intent(context, typeof(AndroidForegroundService));

                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    {
                        context.StartForegroundService(serviceIntent);
                    }
                    else
                    {
                        context.StartService(serviceIntent);
                    }

                    Log.Info("AnonWallClient", "BootReceiver: Service started successfully.");
                }
                else
                {
                    Log.Info("AnonWallClient", "BootReceiver: No LinkId configured, skipping service start.");
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't crash
                Log.Error("AnonWallClient", $"BootReceiver error: {ex.Message}");
            }
        }
    }
}
