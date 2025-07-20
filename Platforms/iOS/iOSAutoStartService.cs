using AnonWallClient.Services;

namespace AnonWallClient.Platforms.iOS;

public class iOSAutoStartService : IAutoStartService
{
    private readonly AppLogService _logger;
    
    public bool IsSupported => false; // iOS doesn't allow true background autostart
    public string PlatformName => "iOS (Not Supported)";

    public iOSAutoStartService(AppLogService logger)
    {
        _logger = logger;
    }

    public async Task<bool> EnableAutoStartAsync(int intervalHours = 1)
    {
        _logger.Add("iOS AutoStart: iOS does not support automatic background app launching due to platform restrictions.");
        return false;
    }

    public async Task<bool> DisableAutoStartAsync()
    {
        _logger.Add("iOS AutoStart: No autostart to disable on iOS.");
        return true;
    }

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        return false;
    }

    public async Task<string> GetAutoStartStatusAsync()
    {
        return "Not supported on iOS - Apple platform restrictions";
    }

    public async Task<bool> UpdateAutoStartIntervalAsync(int intervalHours)
    {
        return false;
    }
}
