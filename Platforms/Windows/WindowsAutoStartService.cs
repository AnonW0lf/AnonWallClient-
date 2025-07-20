using AnonWallClient.Services;

namespace AnonWallClient.Platforms.Windows;

public class WindowsAutoStartService : IAutoStartService
{
    private readonly WindowsTaskSchedulerService _taskSchedulerService;
    private readonly AppLogService _logger;

    public bool IsSupported => true;
    public string PlatformName => "Windows (Task Scheduler)";

    public WindowsAutoStartService(AppLogService logger, SettingsService settingsService)
    {
        _logger = logger;
        _taskSchedulerService = new WindowsTaskSchedulerService(logger, settingsService);
    }

    public async Task<bool> EnableAutoStartAsync(int intervalHours = 1)
    {
        try
        {
            _logger.Add($"Windows AutoStart: Enabling with {intervalHours} hour interval...");
            return await _taskSchedulerService.EnableAutoStartAsync(intervalHours);
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows AutoStart: Error enabling: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DisableAutoStartAsync()
    {
        try
        {
            _logger.Add("Windows AutoStart: Disabling...");
            return await _taskSchedulerService.DisableAutoStartAsync();
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows AutoStart: Error disabling: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        return await _taskSchedulerService.IsAutoStartEnabledAsync();
    }

    public async Task<string> GetAutoStartStatusAsync()
    {
        return await _taskSchedulerService.GetAutoStartStatusAsync();
    }

    public async Task<bool> UpdateAutoStartIntervalAsync(int intervalHours)
    {
        try
        {
            _logger.Add($"Windows AutoStart: Updating interval to {intervalHours} hours...");
            return await _taskSchedulerService.UpdateAutoStartIntervalAsync(intervalHours);
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows AutoStart: Error updating interval: {ex.Message}");
            return false;
        }
    }
}
