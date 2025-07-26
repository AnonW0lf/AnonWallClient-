using AnonWallClient.Services;
using System.Collections.Specialized;
using AnonWallClient.Background;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AnonWallClient.Views;

public partial class HomePage : ContentPage
{
    private readonly AppLogService _logger;
    private readonly PollingService _pollingService;
    private readonly IServiceProvider _serviceProvider;
    private readonly WallpaperHistoryService? _historyService;
    private readonly SettingsService _settingsService;
    private readonly ImageCacheService? _cacheService;
    private readonly IAutoStartService? _autoStartService;
    private bool _isServiceStarted = false;
    private CancellationTokenSource? _statusUpdateCancellation;

    public HomePage(AppLogService logger, PollingService pollingService, IServiceProvider serviceProvider, SettingsService settingsService)
    {
        try
        {
            InitializeComponent();
            _logger = logger;
            _pollingService = pollingService;
            _serviceProvider = serviceProvider;
            _settingsService = settingsService;
            
            // Safe service retrieval with null checks
            try
            {
                _historyService = MauiProgram.Services?.GetService<WallpaperHistoryService>();
            }
            catch (Exception ex)
            {
                _logger?.Add($"Failed to get WallpaperHistoryService: {ex.Message}");
            }
            
            try
            {
                _cacheService = MauiProgram.Services?.GetService<ImageCacheService>();
            }
            catch (Exception ex)
            {
                _logger?.Add($"Failed to get ImageCacheService: {ex.Message}");
            }
            
            try
            {
                _autoStartService = MauiProgram.Services?.GetService<IAutoStartService>();
            }
            catch (Exception ex)
            {
                _logger?.Add($"Failed to get IAutoStartService: {ex.Message}");
            }

            // Safe event subscription
            try
            {
                _logger.Logs.CollectionChanged += OnLogsCollectionChanged;
            }
            catch (Exception ex)
            {
                _logger?.Add($"Failed to subscribe to logs: {ex.Message}");
            }
            
            // Subscribe to polling status changes
            try
            {
                _pollingService.StatusChanged += OnPollingStatusChanged;
            }
            catch (Exception ex)
            {
                _logger?.Add($"Failed to subscribe to polling status: {ex.Message}");
            }
            
            // Subscribe to wallpaper changes to update current wallpaper info
            if (_historyService != null)
            {
                try
                {
                    _historyService.WallpaperAdded += OnWallpaperAdded;
                    _historyService.HistoryCleared += OnHistoryCleared;
                }
                catch (Exception ex)
                {
                    _logger?.Add($"Failed to subscribe to history events: {ex.Message}");
                }
            }
            
            // Safe initialization calls
            try
            {
                LoadCurrentWallpaperInfo();
            }
            catch (Exception ex)
            {
                _logger?.Add($"Error loading wallpaper info: {ex.Message}");
            }
            
            try
            {
                UpdateAllStatusIndicators();
            }
            catch (Exception ex)
            {
                _logger?.Add($"Error updating status indicators: {ex.Message}");
            }
            
            try
            {
                UpdateStatusNotes();
            }
            catch (Exception ex)
            {
                _logger?.Add($"Error updating status notes: {ex.Message}");
            }
            
            // Start periodic status updates with delay to ensure full initialization
            _ = Task.Delay(2000).ContinueWith(_ => StartPeriodicStatusUpdates());
        }
        catch (Exception ex)
        {
            _logger?.Add($"Critical error in HomePage constructor: {ex.Message}");
            // Ensure basic functionality even if some initialization fails
        }
    }

    private void StartPeriodicStatusUpdates()
    {
        try
        {
            _statusUpdateCancellation = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait before starting to ensure UI is fully loaded
                    await Task.Delay(5000, _statusUpdateCancellation.Token);
                    
                    while (!_statusUpdateCancellation.Token.IsCancellationRequested)
                    {
                        await Task.Delay(30000, _statusUpdateCancellation.Token); // 30 seconds
                        
                        if (!_statusUpdateCancellation.Token.IsCancellationRequested)
                        {
                            try
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    try
                                    {
                                        UpdateNetworkStatus();
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger?.Add($"Error in periodic network status update: {ex.Message}");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger?.Add($"Error invoking network status update on main thread: {ex.Message}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (Exception ex)
                {
                    _logger?.Add($"Error in periodic status updates: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.Add($"Error starting periodic status updates: {ex.Message}");
        }
    }

    private void OnWallpaperAdded(object? sender, WallpaperHistoryItem newWallpaper)
    {
        // Update current wallpaper info when a new wallpaper is added
        MainThread.BeginInvokeOnMainThread(() => LoadCurrentWallpaperInfo());
    }

    private void OnHistoryCleared(object? sender, EventArgs e)
    {
        // Clear current wallpaper info when history is cleared
        MainThread.BeginInvokeOnMainThread(() => LoadCurrentWallpaperInfo());
    }

    private async Task ShowToastOrAlertAsync(string message, bool isError = false)
    {
#if ANDROID || IOS || MACCATALYST
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message, isError ? ToastDuration.Long : ToastDuration.Short).Show());
        }
        catch
        {
            await DisplayAlert(isError ? "Error" : "Success", message, "OK");
        }
#else
        await DisplayAlert(isError ? "Error" : "Success", message, "OK");
#endif
    }

    private void LoadCurrentWallpaperInfo()
    {
        try
        {
            var wallpaperManagement = MauiProgram.Services?.GetService<WallpaperManagementService>();
            if (wallpaperManagement != null)
            {
                var (currentWallpaper, currentLockscreen) = wallpaperManagement.GetCurrentWallpapers();
                
                if (currentWallpaper != null)
                {
                    // Show wallpaper thumbnail as primary
                    CurrentWallpaperImage.Source = currentWallpaper.ThumbnailUrl ?? currentWallpaper.ImageUrl;
                    
                    // Enhanced description showing both wallpaper types if applicable
                    var description = currentWallpaper.Description ?? "No description";
                    var setTime = $"Set: {currentWallpaper.SetTime:g}";
                    
                    if (currentLockscreen != null && 
                        currentLockscreen.ImageUrl != currentWallpaper.ImageUrl)
                    {
                        // Different wallpapers for desktop and lockscreen - show combined info
                        CurrentWallpaperDescription.Text = $"🖥️ Desktop: {description}";
                        CurrentWallpaperSetTime.Text = $"{setTime}\n🔒 Lockscreen: {currentLockscreen.Description ?? "No description"} (Set: {currentLockscreen.SetTime:g})";
                        
                        // Create a composite image showing both thumbnails side by side
                        // For now, we'll use the desktop wallpaper as primary with indication that lockscreen differs
                    }
                    else if (currentLockscreen != null)
                    {
                        // Same wallpaper for both desktop and lockscreen
                        CurrentWallpaperDescription.Text = $"🖥️🔒 Both: {description}";
                        CurrentWallpaperSetTime.Text = setTime;
                    }
                    else
                    {
                        // Only desktop wallpaper
                        CurrentWallpaperDescription.Text = $"🖥️ Desktop: {description}";
                        CurrentWallpaperSetTime.Text = setTime;
                    }
                }
                else if (currentLockscreen != null)
                {
                    // Only lockscreen set
                    CurrentWallpaperImage.Source = currentLockscreen.ThumbnailUrl ?? currentLockscreen.ImageUrl;
                    CurrentWallpaperDescription.Text = $"🔒 Lockscreen only: {currentLockscreen.Description ?? "No description"}";
                    CurrentWallpaperSetTime.Text = $"Set: {currentLockscreen.SetTime:g}";
                }
                else
                {
                    // No wallpapers set
                    CurrentWallpaperImage.Source = null;
                    CurrentWallpaperDescription.Text = "No wallpaper set yet.";
                    CurrentWallpaperSetTime.Text = string.Empty;
                }
            }
            else
            {
                // Fallback to old method if service not available
                var current = _historyService?.History?.FirstOrDefault();
                if (current != null)
                {
                    CurrentWallpaperImage.Source = current.ThumbnailUrl ?? current.ImageUrl;
                    var typeText = current.WallpaperType == WallpaperType.Lockscreen ? "🔒 Lockscreen" : "🖥️ Desktop";
                    CurrentWallpaperDescription.Text = $"{typeText}: {current.Description ?? "No description"}";
                    CurrentWallpaperSetTime.Text = $"Set: {current.SetTime:g}";
                }
                else
                {
                    CurrentWallpaperImage.Source = null;
                    CurrentWallpaperDescription.Text = "No wallpaper set yet.";
                    CurrentWallpaperSetTime.Text = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Error loading current wallpaper info: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        try
        {
            // Cancel the periodic updates
            _statusUpdateCancellation?.Cancel();
            _statusUpdateCancellation?.Dispose();
            _statusUpdateCancellation = null;
        }
        catch (Exception ex)
        {
            _logger?.Add($"Error disposing status update cancellation: {ex.Message}");
        }
        
        // Unsubscribe to prevent memory leaks
        if (_historyService != null)
        {
            _historyService.WallpaperAdded -= OnWallpaperAdded;
            _historyService.HistoryCleared -= OnHistoryCleared;
        }
        
        // Unsubscribe from polling status changes
        _pollingService.StatusChanged -= OnPollingStatusChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Re-subscribe when page appears (in case it was unsubscribed)
        if (_historyService != null)
        {
            _historyService.WallpaperAdded -= OnWallpaperAdded; // Remove first to avoid double subscription
            _historyService.WallpaperAdded += OnWallpaperAdded;
            _historyService.HistoryCleared -= OnHistoryCleared;
            _historyService.HistoryCleared += OnHistoryCleared;
        }
        
        LoadCurrentWallpaperInfo();
        UpdateAllStatusIndicators();
        
        // Show restore button only on Windows if we have backups
        ConfigureRestoreButton();

        if (!_isServiceStarted)
        {
            _isServiceStarted = true;

            try
            {
#if ANDROID
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                }

                if (status == PermissionStatus.Granted)
                {
                    var serviceManager = _serviceProvider.GetService<IForegroundServiceManager>();
                    serviceManager?.StartService();
                }
                else
                {
                    _ = Task.Run(() => _pollingService.StartPollingAsync(new CancellationToken()));
                }
#else
                _ = Task.Run(() => _pollingService.StartPollingAsync(new CancellationToken()));
#endif

                var savedLinkId = _settingsService.GetLinkId();
                if (!string.IsNullOrEmpty(savedLinkId))
                {
                    _pollingService.EnablePolling();
                }
                
                // Update status after starting services
                UpdateAllStatusIndicators();
            }
            catch (Exception ex)
            {
                _logger.Add($"Error starting services: {ex.Message}");
            }
        }
    }

    private void ConfigureRestoreButton()
    {
#if WINDOWS
        // Check if we have any backup stored for Windows lockscreen
        try
        {
            var hasBackup = Preferences.Get("OriginalLockScreenImagePath", string.Empty) != string.Empty ||
                           Preferences.Get("OriginalLockScreenImageUrl", string.Empty) != string.Empty ||
                           Preferences.Get("OriginalLockScreenImageStatus", -1) != -1;
            
            RestoreButton.IsVisible = hasBackup;
            
            if (hasBackup)
            {
                _logger.Add("HomePage: Restore button enabled - lockscreen backup detected.");
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"HomePage: Error checking restore availability: {ex.Message}");
        }
#else
        // Hide restore button on non-Windows platforms
        RestoreButton.IsVisible = false;
#endif
    }

    private async void OnRestoreClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.Add("HomePage: Restore button clicked.");
            
            var panicService = MauiProgram.Services?.GetService<PanicService>();
            if (panicService != null)
            {
                var success = await panicService.RestoreOriginalWallpapersAsync();
                if (success)
                {
                    await ShowToastOrAlertAsync("Original wallpapers restored successfully!");
                    
                    // Hide restore button after successful restore
                    RestoreButton.IsVisible = false;
                }
                else
                {
                    await ShowToastOrAlertAsync("Failed to restore original wallpapers. Check logs for details.", true);
                }
            }
            else
            {
                await ShowToastOrAlertAsync("Restore service not available.", true);
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"HomePage: Restore error: {ex.Message}");
            await DisplayAlert("Restore Error", $"{ex.Message}", "OK");
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateStatusNotes();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating status notes: {ex.Message}");
        }
    }

    private async void OnCopyLogClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.Add("Copy log button clicked.");
            
            // Get the full log content first
            var fullLog = await _logger.ExportLogsAsync();
            
            // Check if we have log content
            if (!string.IsNullOrEmpty(fullLog))
            {
                // Calculate size in bytes (UTF-8 encoding)
                var logSizeBytes = System.Text.Encoding.UTF8.GetByteCount(fullLog);
                var logSizeKB = logSizeBytes / 1024;
                var logSizeMB = logSizeKB / 1024.0;
                
                _logger.Add($"Log size: {logSizeKB:N0} KB ({logSizeMB:F2} MB)");
                
                // Android clipboard limit is about 1MB, but let's be conservative at 500KB
                const int maxClipboardSizeBytes = 500 * 1024; // 500KB
                
                if (logSizeBytes > maxClipboardSizeBytes)
                {
                    // Log is too large for clipboard - offer alternatives
                    _logger.Add($"Log too large for clipboard ({logSizeKB:N0} KB > 500 KB limit).");
                    
                    var choice = await DisplayActionSheet(
                        $"Log is too large for clipboard ({logSizeMB:F1} MB).\nChoose what to copy:",
                        "Cancel",
                        null,
                        "📋 Recent status only",
                        "📝 Last 50 log entries",
                        "📄 Log file location");
                    
                    switch (choice)
                    {
                        case "📋 Recent status only":
                            await Clipboard.SetTextAsync(StatusNotesLabel.Text);
                            await ShowToastOrAlertAsync("Recent status copied to clipboard.");
                            break;
                            
                        case "📝 Last 50 log entries":
                            // Get last 50 lines from the full log
                            var lines = fullLog.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                            var lastLines = lines.TakeLast(50).ToArray();
                            var recentLog = string.Join(Environment.NewLine, lastLines);
                            
                            await Clipboard.SetTextAsync(recentLog);
                            await ShowToastOrAlertAsync($"Last {lastLines.Length} log entries copied to clipboard.");
                            break;
                            
                        case "📄 Log file location":
                            var logPath = _logger.GetLogDirectoryPath();
                            await Clipboard.SetTextAsync(logPath);
                            await ShowToastOrAlertAsync($"Log directory path copied: {logPath}");
                            break;
                            
                        default:
                            // User cancelled
                            return;
                    }
                }
                else
                {
                    // Log is small enough for clipboard
                    await Clipboard.SetTextAsync(fullLog);
                    await ShowToastOrAlertAsync($"Full log copied to clipboard ({logSizeKB:N0} KB).");
                }
            }
            else
            {
                // No log file available, fallback to status notes
                if (!string.IsNullOrEmpty(StatusNotesLabel.Text))
                {
                    await Clipboard.SetTextAsync(StatusNotesLabel.Text);
                    await ShowToastOrAlertAsync("Status notes copied to clipboard (no log file found).");
                }
                else
                {
                    await ShowToastOrAlertAsync("No log data available to copy.", true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Error copying log: {ex.Message}");
            
            // Fallback to status notes on any error
            try
            {
                if (!string.IsNullOrEmpty(StatusNotesLabel.Text))
                {
                    await Clipboard.SetTextAsync(StatusNotesLabel.Text);
                    await ShowToastOrAlertAsync("Error with full log - status notes copied instead.");
                }
                else
                {
                    await DisplayAlert("Clipboard Error", 
                        $"Failed to copy log: {ex.Message}\n\nTry using the log directory path option instead.", 
                        "OK");
                }
            }
            catch (Exception fallbackEx)
            {
                await DisplayAlert("Clipboard Error", 
                    $"Failed to copy any log data:\n{ex.Message}\n\nFallback error: {fallbackEx.Message}", 
                    "OK");
            }
        }
    }

    private async void OnPanicClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.Add("UI Panic button clicked.");
            
            var panicService = MauiProgram.Services?.GetService<PanicService>();
            if (panicService != null)
            {
                var success = await panicService.ExecutePanicAsync();
                if (success)
                {
                    await ShowToastOrAlertAsync("Panic wallpaper set successfully!");
                }
                else
                {
                    await ShowToastOrAlertAsync("Failed to set panic wallpaper. Check logs for details.", true);
                }
            }
            else
            {
                // Fallback to old method if service not available
                var panicPath = _settingsService.GetPanicFilePath();
                if (string.IsNullOrEmpty(panicPath)) 
                    panicPath = _settingsService.GetPanicUrl();

                if (!string.IsNullOrEmpty(panicPath) && MauiProgram.Services is not null)
                {
                    var wallpaperService = MauiProgram.Services.GetService<IWallpaperService>();
                    try 
                    { 
                        var success = await wallpaperService?.SetWallpaperAsync(panicPath)!;
                        if (success)
                        {
                            await ShowToastOrAlertAsync("Panic wallpaper set!");
                        }
                        else
                        {
                            await ShowToastOrAlertAsync("Failed to set panic wallpaper.", true);
                        }
                    } 
                    catch (Exception ex)
                    {
                        _logger.Add($"Fallback panic error: {ex.Message}");
                        await ShowToastOrAlertAsync($"Panic error: {ex.Message}", true);
                    }
                }
                else
                {
                    await ShowToastOrAlertAsync("No panic wallpaper configured.", true);
                }
            }
            
            // Don't auto-exit on panic - let user decide
        }
        catch (Exception ex)
        {
            _logger.Add($"Panic error: {ex.Message}");
            await DisplayAlert("Panic Error", $"{ex.Message}", "OK");
        }
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.Add("UI Exit button clicked.");
#if ANDROID
            var serviceManager = _serviceProvider.GetService<IForegroundServiceManager>();
            serviceManager?.StopService();
#endif
            Application.Current?.Quit();
        }
        catch (Exception ex)
        {
            _logger.Add($"Exit error: {ex.Message}");
        }
    }

    private async void OnResponseSelected(object sender, EventArgs e)
    {
        try
        {
            var picker = (Picker)sender;
            var responseType = (string)picker.SelectedItem;

            if (string.IsNullOrWhiteSpace(responseType))
                return;

            var linkId = _settingsService.GetLinkId();
            var apiKey = _settingsService.GetApiKey();

            if (string.IsNullOrWhiteSpace(linkId) || string.IsNullOrWhiteSpace(apiKey))
            {
                await ShowToastOrAlertAsync("ERROR: Link ID and API Key must be set.", true);
                return;
            }

            var (isSuccess, errorMessage) = (false, "Unknown error");
            try
            {
                (isSuccess, errorMessage) = await _pollingService.PostResponseAsync(linkId, apiKey, responseType);
            }
            catch (Exception ex)
            {
                await ShowToastOrAlertAsync($"Network/API error: {ex.Message}", true);
                return;
            }

            if (isSuccess)
            {
                await ShowToastOrAlertAsync("Response Sent!");
            }
            else
            {
                await ShowToastOrAlertAsync($"Failed: {errorMessage}", true);
            }
        }
        catch (Exception ex)
        {
            await ShowToastOrAlertAsync($"Unexpected error: {ex.Message}", true);
        }
    }

    private async void OnSendResponseClicked(object sender, EventArgs e)
    {
        try
        {
            // Check if a response type is selected
            if (ResponseTypePicker.SelectedIndex == -1)
            {
                await ShowToastOrAlertAsync("Please select a response type.", true);
                return;
            }

            // Map picker selection to API response type
            string responseType = ResponseTypePicker.SelectedIndex switch
            {
                0 => "horny",   // Love it (horny)
                1 => "disgust", // Hate it (disgust)
                2 => "came",    // Came
                _ => ""
            };

            if (string.IsNullOrEmpty(responseType))
            {
                await ShowToastOrAlertAsync("Invalid response type selected.", true);
                return;
            }

            var linkId = _settingsService.GetLinkId();
            var apiKey = _settingsService.GetApiKey();

            if (string.IsNullOrWhiteSpace(linkId) || string.IsNullOrWhiteSpace(apiKey))
            {
                await ShowToastOrAlertAsync("ERROR: Link ID and API Key must be set.", true);
                return;
            }

            // Get optional response text
            var responseText = string.IsNullOrWhiteSpace(ResponseTextEntry.Text) ? null : ResponseTextEntry.Text.Trim();

            var (isSuccess, errorMessage) = (false, "Unknown error");
            try
            {
                (isSuccess, errorMessage) = await _pollingService.PostResponseAsync(linkId, apiKey, responseType, responseText);
            }
            catch (Exception ex)
            {
                await ShowToastOrAlertAsync($"Network/API error: {ex.Message}", true);
                return;
            }

            if (isSuccess)
            {
                await ShowToastOrAlertAsync("Response Sent!");
                
                // Clear the text field after successful response
                ResponseTextEntry.Text = string.Empty;
                
                // Handle disgust response - rollback wallpaper
                if (responseType == "disgust")
                {
                    _logger.Add("Disgust response sent - attempting to rollback wallpaper...");
                    
                    var wallpaperManagement = MauiProgram.Services?.GetService<WallpaperManagementService>();
                    if (wallpaperManagement != null)
                    {
                        // Try to rollback both wallpaper types
                        var wallpaperRollback = await wallpaperManagement.RollbackToPreviousWallpaperAsync(WallpaperType.Wallpaper);
                        var lockscreenRollback = await wallpaperManagement.RollbackToPreviousWallpaperAsync(WallpaperType.Lockscreen);
                        
                        if (wallpaperRollback || lockscreenRollback)
                        {
                            await ShowToastOrAlertAsync("Wallpaper rolled back to previous image.");
                            LoadCurrentWallpaperInfo(); // Refresh the display
                        }
                        else
                        {
                            _logger.Add("Could not rollback wallpaper - insufficient history or rollback failed.");
                        }
                    }
                }
            }
            else
            {
                await ShowToastOrAlertAsync($"Failed: {errorMessage}", true);
            }
        }
        catch (Exception ex)
        {
            await ShowToastOrAlertAsync($"Unexpected error: {ex.Message}", true);
        }
    }

    private void OnPollingStatusChanged(object? sender, PollingStatus status)
    {
        MainThread.BeginInvokeOnMainThread(() => UpdatePollingStatus());
    }

    private void UpdatePollingStatus()
    {
        try
        {
            var status = _pollingService.CurrentStatus;
            var isEnabled = _pollingService.IsPollingEnabled;
            
            // Update status indicator color and text
            switch (status)
            {
                case PollingStatus.Running when isEnabled:
                    PollingStatusIndicator.Fill = Colors.Green;
                    PollingStatusLabel.Text = "Running";
                    StopPollingButton.IsVisible = true;
                    StartPollingButton.IsVisible = false;
                    break;
                case PollingStatus.Error:
                    PollingStatusIndicator.Fill = Colors.Orange;
                    PollingStatusLabel.Text = $"Error: {_pollingService.LastError}";
                    StopPollingButton.IsVisible = false;
                    StartPollingButton.IsVisible = true;
                    break;
                default:
                    PollingStatusIndicator.Fill = Colors.Red;
                    PollingStatusLabel.Text = "Stopped";
                    StopPollingButton.IsVisible = false;
                    StartPollingButton.IsVisible = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Error updating polling status: {ex.Message}");
        }
    }

    private void UpdateNetworkStatus()
    {
        try
        {
            var networkAccess = Connectivity.NetworkAccess;
            var profiles = Connectivity.ConnectionProfiles;
            var wifiOnly = _settingsService.GetWifiOnly();
            
            if (networkAccess == NetworkAccess.Internet)
            {
                if (wifiOnly && !profiles.Contains(ConnectionProfile.WiFi))
                {
                    NetworkStatusIndicator.Fill = Colors.Orange;
                    NetworkStatusLabel.Text = "Wi-Fi Only (Not Connected)";
                }
                else if (profiles.Contains(ConnectionProfile.WiFi))
                {
                    NetworkStatusIndicator.Fill = Colors.Green;
                    NetworkStatusLabel.Text = "Wi-Fi Connected";
                }
                else
                {
                    NetworkStatusIndicator.Fill = Colors.LightGreen;
                    NetworkStatusLabel.Text = "Mobile Connected";
                }
            }
            else
            {
                NetworkStatusIndicator.Fill = Colors.Red;
                NetworkStatusLabel.Text = "No Internet";
            }
        }
        catch (Exception ex)
        {
            NetworkStatusIndicator.Fill = Colors.Gray;
            NetworkStatusLabel.Text = "Unknown";
            _logger.Add($"Error updating network status: {ex.Message}");
        }
    }

    private void UpdateCacheStatus()
    {
        try
        {
            if (_cacheService != null)
            {
                var cacheEnabled = _settingsService.GetEnableImageCache();
                if (cacheEnabled)
                {
                    var cacheInfo = _cacheService.GetCacheInfo();
                    CacheStatusIndicator.Fill = Colors.Green;
                    CacheStatusLabel.Text = $"Enabled ({cacheInfo})";
                }
                else
                {
                    CacheStatusIndicator.Fill = Colors.Orange;
                    CacheStatusLabel.Text = "Disabled";
                }
            }
            else
            {
                CacheStatusIndicator.Fill = Colors.Gray;
                CacheStatusLabel.Text = "Service Unavailable";
            }
        }
        catch (Exception ex)
        {
            CacheStatusIndicator.Fill = Colors.Red;
            CacheStatusLabel.Text = "Error";
            _logger.Add($"Error updating cache status: {ex.Message}");
        }
    }

    private async void UpdateAutoStartStatus()
    {
        try
        {
            if (_autoStartService != null && _autoStartService.IsSupported)
            {
                var enabled = _settingsService.GetAutoStartEnabled();
                if (enabled)
                {
                    var status = await _autoStartService.GetAutoStartStatusAsync();
                    AutoStartStatusIndicator.Fill = Colors.Green;
                    AutoStartStatusLabel.Text = $"Enabled ({status})";
                }
                else
                {
                    AutoStartStatusIndicator.Fill = Colors.Orange;
                    AutoStartStatusLabel.Text = "Disabled";
                }
            }
            else
            {
                AutoStartStatusIndicator.Fill = Colors.Gray;
                AutoStartStatusLabel.Text = "Not Supported";
            }
        }
        catch (Exception ex)
        {
            AutoStartStatusIndicator.Fill = Colors.Red;
            AutoStartStatusLabel.Text = "Error";
            _logger.Add($"Error updating autostart status: {ex.Message}");
        }
    }

    private void UpdateConfigurationStatus()
    {
        try
        {
            var hasApiKey = !string.IsNullOrEmpty(_settingsService.GetApiKey());
            var hasWallpaperLinkId = !string.IsNullOrEmpty(_settingsService.GetWallpaperLinkId());
            var hasLockscreenLinkId = !string.IsNullOrEmpty(_settingsService.GetLockscreenLinkId());
            var lockscreenEnabled = _settingsService.GetEnableLockscreenWallpaper();
            
            if (hasApiKey && hasWallpaperLinkId && (hasLockscreenLinkId || !lockscreenEnabled))
            {
                ConfigStatusIndicator.Fill = Colors.Green;
                ConfigStatusLabel.Text = "Complete";
            }
            else if (hasApiKey && (hasWallpaperLinkId || hasLockscreenLinkId))
            {
                ConfigStatusIndicator.Fill = Colors.Orange;
                ConfigStatusLabel.Text = "Partial";
            }
            else
            {
                ConfigStatusIndicator.Fill = Colors.Red;
                ConfigStatusLabel.Text = "Incomplete";
            }
        }
        catch (Exception ex)
        {
            ConfigStatusIndicator.Fill = Colors.Gray;
            ConfigStatusLabel.Text = "Error";
            _logger.Add($"Error updating configuration status: {ex.Message}");
        }
    }

    private void UpdateSystemInfo()
    {
        try
        {
            var info = new List<string>
            {
                $"Platform: {DeviceInfo.Platform}",
                $"Version: {DeviceInfo.VersionString}",
                $"Polling Interval: {_settingsService.GetPollingIntervalSeconds()}s",
                $"History Limit: {(_settingsService.GetMaxHistoryLimit() == 0 ? "Disabled" : _settingsService.GetMaxHistoryLimit().ToString())}",
                $"Lockscreen: {(_settingsService.GetEnableLockscreenWallpaper() ? "Enabled" : "Disabled")}",
                $"Wi-Fi Only: {(_settingsService.GetWifiOnly() ? "Yes" : "No")}"
            };

            SystemInfoLabel.Text = string.Join(" • ", info);
        }
        catch (Exception ex)
        {
            SystemInfoLabel.Text = "System information unavailable";
            _logger.Add($"Error updating system info: {ex.Message}");
        }
    }

    private void UpdateAllStatusIndicators()
    {
        UpdatePollingStatus();
        UpdateNetworkStatus();
        UpdateCacheStatus();
        _ = Task.Run(UpdateAutoStartStatus);
        UpdateConfigurationStatus();
        UpdateSystemInfo();
    }

    private async void OnStopPollingClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.Add("UI Stop Polling button clicked.");
            
            bool confirm = await DisplayAlert("Stop Polling", 
                "Are you sure you want to stop wallpaper polling? The app will stop checking for new wallpapers.", 
                "Yes", "No");
                
            if (confirm)
            {
                _pollingService.DisablePolling();
                await ShowToastOrAlertAsync("Polling stopped. No new wallpapers will be downloaded.");
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Stop polling error: {ex.Message}");
            await DisplayAlert("Error", $"Failed to stop polling: {ex.Message}", "OK");
        }
    }

    private async void OnRefreshStatusClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.Add("UI Refresh Status button clicked.");
            
            await ShowToastOrAlertAsync("Refreshing system status...");
            UpdateAllStatusIndicators();
            
            // Force a network connectivity check
            await Task.Run(() => UpdateNetworkStatus());
            
            await ShowToastOrAlertAsync("Status refreshed successfully.");
        }
        catch (Exception ex)
        {
            _logger.Add($"Refresh status error: {ex.Message}");
            await DisplayAlert("Error", $"Failed to refresh status: {ex.Message}", "OK");
        }
    }

    private void UpdateStatusNotes()
    {
        try
        {
            // Show last 8 log entries as status notes with better formatting
            var recentLogs = _logger.Logs.Take(8).Reverse().ToList();
            
            if (recentLogs.Any())
            {
                // Filter and format important messages
                var formattedLogs = recentLogs
                    .Where(log => !string.IsNullOrWhiteSpace(log))
                    .Select(log => 
                    {
                        // Remove timestamp prefix if present (HH:mm:ss format)
                        var cleaned = System.Text.RegularExpressions.Regex.Replace(log, @"^\d{2}:\d{2}:\d{2} - ", "");
                        
                        // Add icons for different types of messages
                        if (cleaned.Contains("Error") || cleaned.Contains("Failed"))
                            return $"❌ {cleaned}";
                        else if (cleaned.Contains("Success") || cleaned.Contains("completed") || cleaned.Contains("set successfully"))
                            return $"✅ {cleaned}";
                        else if (cleaned.Contains("Warning") || cleaned.Contains("Skipping"))
                            return $"⚠️ {cleaned}";
                        else if (cleaned.Contains("Setting") || cleaned.Contains("Checking"))
                            return $"🔄 {cleaned}";
                        else if (cleaned.Contains("status changed") || cleaned.Contains("enabled") || cleaned.Contains("disabled"))
                            return $"📊 {cleaned}";
                        else
                            return $"ℹ️ {cleaned}";
                    })
                    .ToList();
                
                StatusNotesLabel.Text = string.Join(Environment.NewLine + Environment.NewLine, formattedLogs);
            }
            else
            {
                StatusNotesLabel.Text = "ℹ️ No recent activity";
            }
            
            // Auto-scroll to bottom
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100); // Small delay to ensure UI is updated
                await StatusScrollView.ScrollToAsync(0, StatusNotesLabel.Height, false);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating status notes: {ex.Message}");
            StatusNotesLabel.Text = "❌ Error loading status notes";
        }
    }

    private async void OnStartPollingClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.Add("UI Start Polling button clicked.");
            
            // Check if configuration is complete
            var hasApiKey = !string.IsNullOrEmpty(_settingsService.GetApiKey());
            var hasWallpaperLinkId = !string.IsNullOrEmpty(_settingsService.GetWallpaperLinkId());
            var hasLockscreenLinkId = !string.IsNullOrEmpty(_settingsService.GetLockscreenLinkId());
            var lockscreenEnabled = _settingsService.GetEnableLockscreenWallpaper();
            
            if (!hasApiKey)
            {
                await ShowToastOrAlertAsync("API Key is required. Please configure it in Settings.", true);
                return;
            }
            
            if (!hasWallpaperLinkId && (!hasLockscreenLinkId || !lockscreenEnabled))
            {
                await ShowToastOrAlertAsync("At least one Link ID is required. Please configure it in Settings.", true);
                return;
            }
            
            _pollingService.EnablePolling();
            await ShowToastOrAlertAsync("Polling started. Will check for new wallpapers.");
        }
        catch (Exception ex)
        {
            _logger.Add($"Start polling error: {ex.Message}");
            await DisplayAlert("Error", $"Failed to start polling: {ex.Message}", "OK");
        }
    }
}
