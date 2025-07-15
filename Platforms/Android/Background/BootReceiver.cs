using Android.App;
using Android.Content;
using Android.OS;
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
            var serviceIntent = new Intent(context, typeof(AndroidForegroundService));

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(serviceIntent);
            }
        }
    }
}