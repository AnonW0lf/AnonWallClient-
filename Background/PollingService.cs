using AnonWallClient.Services;

namespace AnonWallClient.Background;

public class PollingService(WalltakerService walltakerService, IWallpaperService wallpaperService, AppLogService logger)
{
    private string _linkId = "";
    private bool _isPollingEnabled = false;

    public void EnablePolling()
    {
        _isPollingEnabled = true;
        logger.Add("Polling has been enabled by user.");
    }

    public async Task<(bool Success, string ErrorMessage)> PostResponseAsync(string linkId, string apiKey, string responseType)
    {
        return await walltakerService.PostResponseAsync(linkId, apiKey, responseType);
    }

    public async Task StartPollingAsync(CancellationToken stoppingToken)
    {
        logger.Add("Service thread started and is idle.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_isPollingEnabled)
            {
                _linkId = Preferences.Get("link_id", string.Empty);

                if (!string.IsNullOrWhiteSpace(_linkId))
                {
                    logger.Add($"Checking for wallpaper with Link ID: {_linkId}");
                    var newImageUrl = await walltakerService.GetNewWallpaperUrlAsync(_linkId);

                    if (newImageUrl != null)
                    {
                        logger.Add($"New wallpaper found: {newImageUrl}");
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await wallpaperService.SetWallpaperAsync(newImageUrl);
                        });
                    }
                    else
                    {
                        logger.Add("No new wallpaper found.");
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}