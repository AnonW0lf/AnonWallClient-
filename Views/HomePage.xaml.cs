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
    private bool _isServiceStarted = false;

    public HomePage(AppLogService logger, PollingService pollingService, IServiceProvider serviceProvider, SettingsService settingsService)
    {
        InitializeComponent();
        _logger = logger;
        _pollingService = pollingService;
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _historyService = MauiProgram.Services?.GetService<WallpaperHistoryService>();

        _logger.Logs.CollectionChanged += OnLogsCollectionChanged;
        
        // Subscribe to wallpaper changes to update current wallpaper info
        if (_historyService != null)
        {
            _historyService.WallpaperAdded += OnWallpaperAdded;
            _historyService.HistoryCleared += OnHistoryCleared;
        }
        
        LogEditor.Text = string.Join(Environment.NewLine, _logger.Logs);
        LoadCurrentWallpaperInfo();
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
        // Unsubscribe to prevent memory leaks
        if (_historyService != null)
        {
            _historyService.WallpaperAdded -= OnWallpaperAdded;
            _historyService.HistoryCleared -= OnHistoryCleared;
        }
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
                LogEditor.Text = string.Join(Environment.NewLine, _logger.Logs);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating logs: {ex.Message}");
        }
    }

    private async void OnCopyLogClicked(object sender, EventArgs e)
    {
        try
        {
            // First try to get full log file content
            var fullLog = await _logger.ExportLogsAsync();
            if (!string.IsNullOrEmpty(fullLog))
            {
                await Clipboard.SetTextAsync(fullLog);
                await ShowToastOrAlertAsync("Full log file copied to clipboard.");
            }
            else
            {
                // Fallback to UI logs
                await Clipboard.SetTextAsync(LogEditor.Text);
                await ShowToastOrAlertAsync("UI log copied to clipboard.");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Clipboard Error", $"Failed to copy log: {ex.Message}", "OK");
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
}
