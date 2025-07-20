using AnonWallClient.Services;

namespace AnonWallClient.Background;

public class PollingService(WalltakerService walltakerService, IWallpaperService wallpaperService, AppLogService logger, SettingsService settingsService)
{
    private string _linkId = "";
    private bool _isPollingEnabled = false;
    private readonly SettingsService _settingsService = settingsService;

    public void EnablePolling()
    {
        _isPollingEnabled = true;
        logger.Add("Polling has been enabled.");
    }

    public void DisablePolling()
    {
        _isPollingEnabled = false;
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
                    _linkId = _settingsService.GetLinkId();

                    if (!string.IsNullOrWhiteSpace(_linkId))
                    {
                        // Check network connectivity before making API calls
                        if (!IsNetworkAvailable())
                        {
                            logger.Add("Skipping wallpaper check due to network restrictions.");
                        }
                        else
                        {
                            logger.Add($"Checking for wallpaper with Link ID: {_linkId}");
                            
                            try
                            {
                                var newImageUrl = await walltakerService.GetNewWallpaperUrlAsync(_linkId);

                                if (newImageUrl != null)
                                {
                                    logger.Add($"New wallpaper found: {newImageUrl}");
                                    try
                                    {
                                        var success = await wallpaperService.SetWallpaperAsync(newImageUrl);
                                        if (!success)
                                        {
                                            logger.Add("Failed to set wallpaper.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.Add($"Error setting wallpaper: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    logger.Add("No new wallpaper found.");
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Add($"Error checking for new wallpaper: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        logger.Add("Link ID not set, skipping wallpaper check.");
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
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait longer on error
            }
        }
        
        logger.Add("Polling service has exited.");
    }
}