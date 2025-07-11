using AnonWallClient.Services;

namespace AnonWallClient.Background;

public class PollingService
{
    private readonly WalltakerService _walltakerService;
    private readonly IWallpaperService _wallpaperService;
    private readonly AppLogService _logger;
    private string _linkId = "";
    private bool _isPollingEnabled = false; // The service is disabled by default

    public PollingService(WalltakerService walltakerService, IWallpaperService wallpaperService, AppLogService logger)
    {
        _walltakerService = walltakerService;
        _wallpaperService = wallpaperService;
        _logger = logger;
    }

    // This method allows the UI to activate the polling loop
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
            // Only do work if the polling has been enabled
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