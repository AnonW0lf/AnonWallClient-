using AnonWallClient.Views;
using AnonWallClient.Services;
using AnonWallClient.Background;

namespace AnonWallClient;

public partial class AppShell : Shell
{
    private readonly SettingsService? _settingsService;

    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("HistoryPage", typeof(HistoryPage));
        Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
        
        _settingsService = MauiProgram.Services?.GetService<SettingsService>();
        
        // Update profile tab visibility when appearing
        UpdateProfileTabVisibility();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Update profile tab visibility
            UpdateProfileTabVisibility();

            // Check if a Link ID has been saved before
            var savedLinkId = _settingsService?.GetLinkId() ?? string.Empty;

            // If no Link ID is found, force the user to the Settings page
            if (string.IsNullOrEmpty(savedLinkId))
            {
                try
                {
                    await Current.GoToAsync("//SettingsPage");
                }
                catch (Exception navEx)
                {
                    await Shell.Current.DisplayAlert("Navigation Error", $"Failed to navigate: {navEx.Message}", "OK");
                }
            }
            else
            {
                // We have configuration, trigger initial wallpaper setting if configured
                await TriggerInitialWallpaperSettingAsync();
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Startup Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private static bool _isInitialTriggerInProgress = false;
    private static DateTime _lastTriggerAttempt = DateTime.MinValue;

    private async Task TriggerInitialWallpaperSettingAsync()
    {
        try
        {
            if (_settingsService == null || _isInitialTriggerInProgress) return;
            
            // Prevent rapid successive triggers (e.g., from UAC spawned instances)
            if (DateTime.Now - _lastTriggerAttempt < TimeSpan.FromSeconds(10))
            {
                var logger = MauiProgram.Services?.GetService<AppLogService>();
                logger?.Add("AppShell: Skipping initial trigger - too soon after last attempt (likely UAC spawn).");
                return;
            }
            
            _lastTriggerAttempt = DateTime.Now;
            _isInitialTriggerInProgress = true;

            var wallpaperLinkId = _settingsService.GetWallpaperLinkId();
            var lockscreenLinkId = _settingsService.GetLockscreenLinkId();
            var apiKey = _settingsService.GetApiKey();

            // Only trigger if we have valid configuration and API key
            if (string.IsNullOrEmpty(apiKey)) 
            {
                _isInitialTriggerInProgress = false;
                return;
            }

            var pollingService = MauiProgram.Services?.GetService<PollingService>();
            if (pollingService == null) 
            {
                _isInitialTriggerInProgress = false;
                return;
            }

            var logService = MauiProgram.Services?.GetService<AppLogService>();
            logService?.Add("AppShell: Triggering initial wallpaper check on startup...");

            // Add a small delay to let the app fully initialize
            await Task.Delay(2000);

            // Enable polling which will trigger initial wallpaper setting
            pollingService.EnablePolling();
            
            // Reset the flag after a delay
            _ = Task.Delay(5000).ContinueWith(_ => _isInitialTriggerInProgress = false);
        }
        catch (Exception ex)
        {
            var logService = MauiProgram.Services?.GetService<AppLogService>();
            logService?.Add($"AppShell: Error triggering initial wallpaper setting: {ex.Message}");
            _isInitialTriggerInProgress = false;
        }
    }

    public void UpdateProfileTabVisibility()
    {
        try
        {
            if (_settingsService != null && ProfileTab != null)
            {
                var apiKey = _settingsService.GetApiKey();
                var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);
                
                ProfileTab.IsVisible = hasApiKey;
                
                // Log the visibility change for debugging
                var logger = MauiProgram.Services?.GetService<AppLogService>();
                logger?.Add($"AppShell: Profile tab visibility set to {hasApiKey}");
            }
        }
        catch (Exception ex)
        {
            var logger = MauiProgram.Services?.GetService<AppLogService>();
            logger?.Add($"AppShell: Error updating profile tab visibility: {ex.Message}");
        }
    }
}
