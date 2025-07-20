using AnonWallClient.Services;

namespace AnonWallClient.Platforms.Android;

public class AndroidAutoStartService : IAutoStartService
{
    private readonly AppLogService _logger;
    
    public bool IsSupported => true;
    public string PlatformName => "Android (Boot Receiver)";

    public AndroidAutoStartService(AppLogService logger)
    {
        _logger = logger;
    }

    public async Task<bool> EnableAutoStartAsync(int intervalHours = 1)
    {
        try
        {
            _logger.Add("Android AutoStart: Boot receiver is already configured in manifest.");
            _logger.Add($"Android AutoStart: Interval setting ({intervalHours} hours) saved for when service starts.");
            
            // Store the interval preference for when the service starts
            Preferences.Set("AutoStartIntervalHours", intervalHours);
            Preferences.Set("AutoStartEnabled", true);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"Android AutoStart: Error enabling: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DisableAutoStartAsync()
    {
        try
        {
            _logger.Add("Android AutoStart: Disabling boot receiver functionality...");
            Preferences.Set("AutoStartEnabled", false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"Android AutoStart: Error disabling: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        return Preferences.Get("AutoStartEnabled", false);
    }

    public async Task<string> GetAutoStartStatusAsync()
    {
        try
        {
            var enabled = Preferences.Get("AutoStartEnabled", false);
            if (!enabled)
            {
                return "Disabled";
            }

            var intervalHours = Preferences.Get("AutoStartIntervalHours", 1);
            return $"Enabled (Boot receiver configured, polling every {intervalHours} hours)";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> UpdateAutoStartIntervalAsync(int intervalHours)
    {
        try
        {
            _logger.Add($"Android AutoStart: Updating interval to {intervalHours} hours...");
            Preferences.Set("AutoStartIntervalHours", intervalHours);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"Android AutoStart: Error updating interval: {ex.Message}");
            return false;
        }
    }
}
