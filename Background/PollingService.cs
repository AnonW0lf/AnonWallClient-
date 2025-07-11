using AnonWallClient.Services;

namespace AnonWallClient.Background;

public class PollingService
{
    private readonly WalltakerService _walltakerService;
    private readonly IWallpaperService _wallpaperService;
    private readonly AppLogService _logger;
    private string _linkId = "";
    private bool _isPollingEnabled = false; // Add this flag, default to false

    public PollingService(WalltakerService walltakerService, IWallpaperService wallpaperService, AppLogService logger)
    {
        _walltakerService = walltakerService;
        _wallpaperService = wallpaperService;
        _logger = logger;
    }

    // Add this public method to allow the UI to start the polling
    public void EnablePolling()
    {
        _isPollingEnabled = true;
        _logger.Add("Polling has been enabled.");
    }

    public async Task StartPollingAsync(CancellationToken stoppingToken)
    {
        _logger.Add("Service started and is idle.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // THE FIX: The service will now only check for wallpapers if it has been enabled
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

            // The loop still waits, but may not do any work
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}