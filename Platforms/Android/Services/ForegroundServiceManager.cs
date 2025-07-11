using Android.Content;
using Android.OS;
using AnonWallClient.Platforms.Android.Background;
using AnonWallClient.Services;

namespace AnonWallClient.Platforms.Android.Services;

public class ForegroundServiceManager : IForegroundServiceManager
{
    public void StartService()
    {
        var intent = new Intent(global::Android.App.Application.Context, typeof(AndroidForegroundService));

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            global::Android.App.Application.Context.StartForegroundService(intent);
        }
    }

    public void StopService()
    {
        var intent = new Intent(global::Android.App.Application.Context, typeof(AndroidForegroundService));
        global::Android.App.Application.Context.StopService(intent);
    }
}