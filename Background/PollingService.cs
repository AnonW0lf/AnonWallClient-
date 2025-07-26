using AnonWallClient.Services;

namespace AnonWallClient.Background;

public enum PollingStatus
{
    Stopped,
    Running,
    Error
}

public class PollingService(WalltakerService walltakerService, IWallpaperService wallpaperService, AppLogService logger, SettingsService settingsService)
{
    private bool _isPollingEnabled = false;
    private PollingStatus _currentStatus = PollingStatus.Stopped;
    private string _lastError = string.Empty;
    private readonly SettingsService _settingsService = settingsService;

    public PollingStatus CurrentStatus => _currentStatus;
    public string LastError => _lastError;
    public bool IsPollingEnabled => _isPollingEnabled;

    public event EventHandler<PollingStatus>? StatusChanged;

    private void SetStatus(PollingStatus status, string error = "")
    {
        if (_currentStatus != status)
        {
            _currentStatus = status;
            _lastError = error;
            StatusChanged?.Invoke(this, status);
            
            var statusText = status switch
            {
                PollingStatus.Running => "Running",
                PollingStatus.Error => $"Error: {error}",
                _ => "Stopped"
            };
            logger.Add($"Polling status changed to: {statusText}");
        }
    }

    public void EnablePolling()
    {
        _isPollingEnabled = true;
        SetStatus(PollingStatus.Running);
        logger.Add("Polling has been enabled.");
    }

    public void DisablePolling()
    {
        _isPollingEnabled = false;
        SetStatus(PollingStatus.Stopped);
        logger.Add("Polling has been disabled.");
    }

    public async Task<(bool Success, string ErrorMessage)> PostResponseAsync(string linkId, string apiKey, string responseType, string? responseText = null)
    {
        try
        {
            // Check network connectivity before making API calls
            if (!IsNetworkAvailable())
            {
                return (false, "Network not available or Wi-Fi only mode enabled without Wi-Fi connection");
            }
            
            return await walltakerService.PostResponseAsync(linkId, apiKey, responseType, responseText);
        }
        catch (Exception ex)
        {
            logger.Add($"Error posting response: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private bool IsNetworkAvailable()
    {
        try
        {
            var current = Connectivity.NetworkAccess;
            logger.Add($"Network access status: {current}");
            
            if (current != NetworkAccess.Internet)
            {
                logger.Add($"No internet access. Current state: {current}");
                return false;
            }

            // Check Wi-Fi only setting
            var wifiOnly = _settingsService.GetWifiOnly();
            logger.Add($"Wi-Fi only mode: {(wifiOnly ? "enabled" : "disabled")}");
            
            if (wifiOnly)
            {
                var profiles = Connectivity.ConnectionProfiles;
                logger.Add($"Connection profiles: {string.Join(", ", profiles)}");
                
                var hasWifi = profiles.Contains(ConnectionProfile.WiFi);
                
                if (!hasWifi)
                {
                    logger.Add("Wi-Fi only mode enabled, but not connected to Wi-Fi. Skipping API call.");
                    return false;
                }
                
                logger.Add("Wi-Fi only mode enabled and connected to Wi-Fi.");
            }
            else
            {
                logger.Add("Wi-Fi only mode disabled, allowing all connection types.");
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.Add($"Error checking network connectivity: {ex.Message}");
            return false;
        }
    }

    public async Task StartPollingAsync(CancellationToken stoppingToken)
    {
        logger.Add("Service thread started and is idle.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_isPollingEnabled)
                {
                    // Get LinkId settings
                    var linkIdMode = _settingsService.GetLinkIdMode();
                    var wallpaperLinkId = _settingsService.GetWallpaperLinkId();
                    var lockscreenLinkId = _settingsService.GetLockscreenLinkId();

                    // Check if we have at least one valid LinkId
                    var hasValidLinkId = !string.IsNullOrWhiteSpace(wallpaperLinkId) || !string.IsNullOrWhiteSpace(lockscreenLinkId);

                    if (hasValidLinkId)
                    {
                        // Check network connectivity before making API calls
                        if (!IsNetworkAvailable())
                        {
                            logger.Add("Skipping wallpaper check due to network restrictions.");
                        }
                        else
                        {
                            logger.Add($"Checking for wallpapers - Mode: {linkIdMode}, " +
                                      $"Wallpaper LinkId: {(!string.IsNullOrEmpty(wallpaperLinkId) ? wallpaperLinkId : "Not set")}, " +
                                      $"Lockscreen LinkId: {(!string.IsNullOrEmpty(lockscreenLinkId) ? lockscreenLinkId : "Not set")}");
                            
                            try
                            {
                                // Check for new wallpapers using the new multi-LinkId method
                                var newWallpapers = await walltakerService.CheckMultipleLinkIdsAsync(
                                    wallpaperLinkId, lockscreenLinkId, linkIdMode);

                                if (newWallpapers.Any())
                                {
                                    logger.Add($"Found {newWallpapers.Count} new wallpaper(s)");
                                    
                                    // Process each new wallpaper
                                    foreach (var (imageUrl, wallpaperType) in newWallpapers)
                                    {
                                        // Check if lockscreen is enabled when processing lockscreen wallpapers
                                        if (wallpaperType == WallpaperType.Lockscreen && !_settingsService.GetEnableLockscreenWallpaper())
                                        {
                                            logger.Add($"Skipping lockscreen wallpaper (lockscreen disabled): {imageUrl}");
                                            continue;
                                        }

                                        logger.Add($"Setting new {wallpaperType}: {imageUrl}");
                                        try
                                        {
                                            var success = await wallpaperService.SetWallpaperAsync(
                                                imageUrl, 
                                                _settingsService.GetWallpaperFitMode(), 
                                                wallpaperType);
                                                
                                            if (!success)
                                            {
                                                logger.Add($"Failed to set {wallpaperType}.");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.Add($"Error setting {wallpaperType}: {ex.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    logger.Add("No new wallpapers found.");
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Add($"Error checking for new wallpapers: {ex.Message}");
                                SetStatus(PollingStatus.Error, ex.Message);
                            }
                        }
                    }
                    else
                    {
                        logger.Add("No Link IDs set, skipping wallpaper check.");
                    }
                }

                var pollingInterval = _settingsService.GetPollingIntervalSeconds();
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, pollingInterval)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.Add("Polling service stopped.");
                break;
            }
            catch (Exception ex)
            {
                logger.Add($"Unexpected error in polling loop: {ex.Message}");
                SetStatus(PollingStatus.Error, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait longer on error
            }
        }
        
        logger.Add("Polling service has exited.");
    }
}
