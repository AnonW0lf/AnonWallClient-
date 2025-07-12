using AnonWallClient.Services;

namespace AnonWallClient.Background;

public class PollingService(WalltakerService walltakerService, IWallpaperService wallpaperService, AppLogService logger)
{
    private readonly WalltakerService _walltakerService = walltakerService;
    private readonly IWallpaperService _wallpaperService = wallpaperService;
    private readonly AppLogService _logger = logger;
    private string _linkId = "";
    private bool _isPollingEnabled = false;

    public void EnablePolling()
    {
        _isPollingEnabled = true;
        _logger.Add("Polling has been enabled by user.");
    }

    public async Task StartPollingAsync(CancellationToken stoppingToken)
    {
        _logger.Add("Service thread started and is idle.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_isPollingEnabled)
            {
                _linkId = Preferences.Get("link_id", string.Empty);

                if (!string.IsNullOrWhiteSpace(_linkId))
                {
                    _logger.Add($"Checking for wallpaper with Link ID: {_linkId}");
                    var newImageUrl = await _walltakerService.GetNewWallpaperUrlAsync(_linkId);

                    if (newImageUrl != null)
                    {
                        _logger.Add($"New wallpaper found: {newImageUrl}");
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await _wallpaperService.SetWallpaperAsync(newImageUrl);
                        });
                    }
                    else
                    {
                        _logger.Add("No new wallpaper found.");
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}