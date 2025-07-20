using AnonWallClient.Services;
using AnonWallClient.Background;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Collections.Generic;
using System.Diagnostics;

namespace AnonWallClient.Views;

#if ANDROID
public class ReadMediaImagesPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new List<(string androidPermission, bool isRuntime)>
        {
            (global::Android.Manifest.Permission.ReadMediaImages, true)
        }.ToArray();
}

public class ExternalStoragePermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions
    {
        get
        {
            var permissions = new List<(string androidPermission, bool isRuntime)>();
            
            // For Android 13+ (API 33+), use READ_MEDIA_IMAGES
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                permissions.Add((Android.Manifest.Permission.ReadMediaImages, true));
            }
            // For Android 10-12 (API 29-32), use READ_EXTERNAL_STORAGE
            else if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
            {
                permissions.Add((Android.Manifest.Permission.ReadExternalStorage, true));
            }
            // For Android 9 and below (API 28 and below), use both READ and WRITE
            else
            {
                permissions.Add((Android.Manifest.Permission.ReadExternalStorage, true));
                permissions.Add((Android.Manifest.Permission.WriteExternalStorage, true));
            }
            
            return permissions.ToArray();
        }
    }
}

// Helper method to check if we can access external storage
public static class AndroidStorageHelper
{
    public static async Task<bool> CheckAndRequestStoragePermissionAsync()
    {
        try
        {
            // For Android 11+ (API 30+), we need MANAGE_EXTERNAL_STORAGE for full access
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                // Check if we have MANAGE_EXTERNAL_STORAGE permission
                var hasManagePermission = Android.OS.Environment.IsExternalStorageManager;
                if (!hasManagePermission)
                {
                    // Request user to grant permission through settings
                    var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                    intent.SetData(Android.Net.Uri.Parse($"package:{Android.App.Application.Context.PackageName}"));
                    intent.SetFlags(Android.Content.ActivityFlags.NewTask);
                    
                    Android.App.Application.Context.StartActivity(intent);
                    return false; // User needs to manually grant permission
                }
                return true;
            }
            else
            {
                // For older versions, use standard permission system
                var status = await Permissions.RequestAsync<ExternalStoragePermission>();
                return status == PermissionStatus.Granted;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking storage permissions: {ex.Message}");
            return false;
        }
    }
}
#endif

public partial class SettingsPage : ContentPage
{
    private readonly PollingService _pollingService;
    private readonly AppLogService _logger;
    private readonly SettingsService _settingsService;
    private readonly ImageCacheService _cacheService;
    private readonly IAutoStartService? _autoStartService;

    public SettingsPage(PollingService pollingService, AppLogService logger, SettingsService settingsService, ImageCacheService cacheService)
    {
        InitializeComponent();
        _pollingService = pollingService;
        _logger = logger;
        _settingsService = settingsService;
        _cacheService = cacheService;
        
        // Get autostart service if available
        _autoStartService = MauiProgram.Services?.GetService<IAutoStartService>();
        
        SetupEventHandlers();
        LoadSettings();
    }

    private void SetupEventHandlers()
    {
        EnableCacheCheckBox.CheckedChanged += OnEnableCacheChanged;
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

    private async void LoadSettings()
    {
        try
        {
            // Load Link ID settings
            var linkIdMode = _settingsService.GetLinkIdMode();
            LinkIdModePicker.SelectedIndex = (int)linkIdMode;
            
            SharedLinkIdEntry.Text = _settingsService.GetSharedLinkId();
            WallpaperLinkIdEntry.Text = _settingsService.GetWallpaperLinkId();
            LockscreenLinkIdEntry.Text = _settingsService.GetLockscreenLinkId();
            
            UpdateLinkIdSectionsVisibility();

            // Load other settings
            ApiKeyEntry.Text = _settingsService.GetApiKey();
            
            // Load wallpaper fit mode
            WallpaperFitModePicker.SelectedIndex = (int)_settingsService.GetWallpaperFitMode();
            
            // Load caching settings
            EnableCacheCheckBox.IsChecked = _settingsService.GetEnableImageCache();
            MaxCacheSizeEntry.Text = _settingsService.GetMaxCacheSizeMB().ToString();
            CacheExpiryEntry.Text = _settingsService.GetCacheExpiryDays().ToString();
            UpdateCacheInfo();
            UpdateCacheOptionsVisibility();
            
            // Load autostart settings
            await LoadAutoStartSettingsAsync();
            
            // Load polling settings
            PollingIntervalEntry.Text = _settingsService.GetPollingIntervalSeconds().ToString();
            WifiOnlyCheckBox.IsChecked = _settingsService.GetWifiOnly();
            
            // Load history settings
            MaxHistoryEntry.Text = _settingsService.GetMaxHistoryLimit().ToString();
            
            // Load panic settings
            var savedPanicFile = _settingsService.GetPanicFilePath();
            PanicFileLabel.Text = string.IsNullOrEmpty(savedPanicFile) ? "No local file selected." : "Local file set!";
            PanicUrlEntry.Text = _settingsService.GetPanicUrl();
            
            // Load save folder setting
            var saveFolder = _settingsService.GetWallpaperSaveFolder();
            SaveFolderLabel.Text = string.IsNullOrEmpty(saveFolder) ? "Default folder will be used" : saveFolder;
        }
        catch (Exception ex)
        {
            _logger.Add($"Error loading settings: {ex.Message}");
            DisplayAlert("Error", $"Failed to load settings: {ex.Message}", "OK");
        }
    }

    private async Task LoadAutoStartSettingsAsync()
    {
        try
        {
            if (_autoStartService == null)
            {
                // Hide autostart section if service not available
                AutoStartSection.IsVisible = false;
                return;
            }

            // Show/configure autostart section
            AutoStartSection.IsVisible = true;
            AutoStartPlatformLabel.Text = $"Platform: {_autoStartService.PlatformName}";
            
            if (!_autoStartService.IsSupported)
            {
                AutoStartEnabledCheckBox.IsEnabled = false;
                AutoStartLabel.Text = "Auto-start not supported on this platform";
                AutoStartStatusLabel.Text = "Status: Not supported";
                return;
            }

            // Load settings
            AutoStartEnabledCheckBox.IsChecked = _settingsService.GetAutoStartEnabled();
            AutoStartIntervalEntry.Text = _settingsService.GetAutoStartIntervalHours().ToString();
            
            // Update UI visibility and status
            UpdateAutoStartOptionsVisibility();
            await RefreshAutoStartStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.Add($"Error loading autostart settings: {ex.Message}");
            AutoStartStatusLabel.Text = $"Status: Error - {ex.Message}";
        }
    }

    private void OnAutoStartEnabledChanged(object sender, CheckedChangedEventArgs e)
    {
        UpdateAutoStartOptionsVisibility();
    }

    private void UpdateAutoStartOptionsVisibility()
    {
        AutoStartOptionsSection.IsVisible = AutoStartEnabledCheckBox.IsChecked && _autoStartService?.IsSupported == true;
    }

    private async void OnTestAutoStartClicked(object sender, EventArgs e)
    {
        if (_autoStartService == null)
        {
            await ShowToastOrAlertAsync("AutoStart service not available.", true);
            return;
        }

        try
        {
            TestAutoStartButton.IsEnabled = false;
            TestAutoStartButton.Text = "Testing...";

            var intervalHours = int.TryParse(AutoStartIntervalEntry.Text, out int interval) ? interval : 1;
            
            _logger.Add("Testing autostart configuration...");
            var success = await _autoStartService.EnableAutoStartAsync(intervalHours);
            
            if (success)
            {
                await ShowToastOrAlertAsync("AutoStart test successful! Configuration applied.");
                await RefreshAutoStartStatusAsync();
            }
            else
            {
                await ShowToastOrAlertAsync("AutoStart test failed. Check logs for details.", true);
            }
        }
        catch (Exception ex)
        {
            await ShowToastOrAlertAsync($"AutoStart test error: {ex.Message}", true);
        }
        finally
        {
            TestAutoStartButton.IsEnabled = true;
            TestAutoStartButton.Text = "Test Configuration";
        }
    }

    private async void OnRefreshAutoStartStatusClicked(object sender, EventArgs e)
    {
        await RefreshAutoStartStatusAsync();
    }

    private async Task RefreshAutoStartStatusAsync()
    {
        try
        {
            if (_autoStartService != null)
            {
                var status = await _autoStartService.GetAutoStartStatusAsync();
                AutoStartStatusLabel.Text = $"Status: {status}";
            }
        }
        catch (Exception ex)
        {
            AutoStartStatusLabel.Text = $"Status: Error - {ex.Message}";
        }
    }

    private void OnLinkIdModeChanged(object sender, EventArgs e)
    {
        UpdateLinkIdSectionsVisibility();
    }

    private void UpdateLinkIdSectionsVisibility()
    {
        var isSharedMode = LinkIdModePicker.SelectedIndex == 0;
        SharedLinkIdSection.IsVisible = isSharedMode;
        SeparateLinkIdsSection.IsVisible = !isSharedMode;
    }

    private void OnEnableCacheChanged(object sender, CheckedChangedEventArgs e)
    {
        UpdateCacheOptionsVisibility();
    }

    private void UpdateCacheOptionsVisibility()
    {
        CacheOptionsSection.IsVisible = EnableCacheCheckBox.IsChecked;
    }

    private void UpdateCacheInfo()
    {
        try
        {
            CacheInfoLabel.Text = _cacheService.GetCacheInfo();
        }
        catch (Exception ex)
        {
            _logger.Add($"Error updating cache info: {ex.Message}");
            CacheInfoLabel.Text = "Cache info unavailable";
        }
    }

    private async void OnClearCacheClicked(object sender, EventArgs e)
    {
        try
        {
            bool confirm = await DisplayAlert("Confirm Clear Cache", 
                "Are you sure you want to clear the image cache? This will remove all cached images and may result in slower performance until images are re-downloaded.", 
                "Yes", "No");
                
            if (confirm)
            {
                await _cacheService.ClearCacheAsync();
                UpdateCacheInfo();
                await ShowToastOrAlertAsync("Image cache cleared successfully.");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to clear cache: {ex.Message}", "OK");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            // Capture old fit mode for comparison
            var oldFitMode = _settingsService.GetWallpaperFitMode();

            // Save Link ID settings
            var linkIdMode = (LinkIdMode)LinkIdModePicker.SelectedIndex;
            _settingsService.SetLinkIdMode(linkIdMode);

            if (linkIdMode == LinkIdMode.SharedLink)
            {
                _settingsService.SetSharedLinkId(SharedLinkIdEntry.Text);
            }
            else
            {
                _settingsService.SetWallpaperLinkId(WallpaperLinkIdEntry.Text);
                _settingsService.SetLockscreenLinkId(LockscreenLinkIdEntry.Text);
            }

            // Save API key
            _settingsService.SetApiKey(ApiKeyEntry.Text);
            
            // Save wallpaper fit mode
            var newFitMode = oldFitMode;
            if (WallpaperFitModePicker.SelectedIndex >= 0)
            {
                newFitMode = (WallpaperFitMode)WallpaperFitModePicker.SelectedIndex;
                _settingsService.SetWallpaperFitMode(newFitMode);
            }
            
            // Save caching settings
            _settingsService.SetEnableImageCache(EnableCacheCheckBox.IsChecked);
            
            if (int.TryParse(MaxCacheSizeEntry.Text, out int maxCacheSize) && maxCacheSize >= 10 && maxCacheSize <= 1000)
            {
                _settingsService.SetMaxCacheSizeMB(maxCacheSize);
            }
            else
            {
                await DisplayAlert("Invalid Input", "Max cache size must be between 10 and 1000 MB.", "OK");
                return;
            }
            
            if (int.TryParse(CacheExpiryEntry.Text, out int cacheExpiry) && cacheExpiry >= 1 && cacheExpiry <= 30)
            {
                _settingsService.SetCacheExpiryDays(cacheExpiry);
            }
            else
            {
                await DisplayAlert("Invalid Input", "Cache expiry must be between 1 and 30 days.", "OK");
                return;
            }
            
            // Save and apply autostart settings
            await SaveAutoStartSettingsAsync();
            
            // Save polling settings
            if (int.TryParse(PollingIntervalEntry.Text, out int interval) && interval > 0)
            {
                _settingsService.SetPollingIntervalSeconds(interval);
            }
            else
            {
                await DisplayAlert("Invalid Input", "Polling interval must be a positive number.", "OK");
                return;
            }
            
            // Save history settings
            if (int.TryParse(MaxHistoryEntry.Text, out int maxHistory) && maxHistory >= 0)
            {
                var currentLimit = _settingsService.GetMaxHistoryLimit();
                _settingsService.SetMaxHistoryLimit(maxHistory);
                
                // If the limit was reduced or disabled, trigger a trim of existing history
                if (maxHistory < currentLimit || maxHistory == 0)
                {
                    var historyService = MauiProgram.Services?.GetService<WallpaperHistoryService>();
                    historyService?.Load(); // This will trigger TrimHistoryToLimit
                }
            }
            else
            {
                await DisplayAlert("Invalid Input", "Max history limit must be 0 or a positive number.", "OK");
                return;
            }
            
            // Save panic settings
            _settingsService.SetPanicUrl(PanicUrlEntry.Text);
            _settingsService.SetWifiOnly(WifiOnlyCheckBox.IsChecked);

            // Reapply wallpapers if fit mode changed
            await ReapplyWallpapersIfNeededAsync(oldFitMode, newFitMode);

            // Enable polling
            _pollingService.EnablePolling();

            // Trigger immediate wallpaper setting if lockscreen is configured
            await TriggerImmediateWallpaperSettingAsync();

            // Update profile tab visibility in AppShell
            if (Application.Current?.MainPage is AppShell appShell)
            {
                appShell.UpdateProfileTabVisibility();
            }

            await ShowToastOrAlertAsync("Settings Saved! Polling is enabled.");
            
            // Show additional info about AutoStart if it's enabled but not applied
            if (_autoStartService?.IsSupported == true && _settingsService.GetAutoStartEnabled())
            {
                await ShowToastOrAlertAsync("Note: AutoStart settings saved. Use 'Test Configuration' button to apply AutoStart changes.", false);
            }

            try
            {
                await Shell.Current.GoToAsync("//HomePage");
            }
            catch (Exception navEx)
            {
                await DisplayAlert("Navigation Error", $"Failed to navigate: {navEx.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
        }
    }

    private async Task SaveAutoStartSettingsAsync()
    {
        try
        {
            if (_autoStartService == null || !_autoStartService.IsSupported)
            {
                return; // Skip if not supported
            }

            var enabled = AutoStartEnabledCheckBox.IsChecked;
            var intervalHours = int.TryParse(AutoStartIntervalEntry.Text, out int interval) && interval >= 1 && interval <= 24 ? interval : 1;

            // Save to settings first
            _settingsService.SetAutoStartEnabled(enabled);
            _settingsService.SetAutoStartIntervalHours(intervalHours);

            // TEMPORARILY DISABLE AutoStart operations during settings save to prevent UAC/app spawning issues
            // The user can use the "Test Configuration" button to manually apply autostart if needed
            _logger.Add("SaveAutoStartSettingsAsync: AutoStart configuration saved to settings only (UAC operations disabled to prevent app spawning).");
            
            /*
            // ORIGINAL CODE - Causes UAC prompts and app spawning:
            
            // Apply autostart configuration
            if (enabled)
            {
                _logger.Add($"Applying autostart configuration: {intervalHours} hour interval...");
                var success = await _autoStartService.EnableAutoStartAsync(intervalHours);
                if (!success)
                {
                    await ShowToastOrAlertAsync("Warning: AutoStart configuration may have failed. Check logs for details.", true);
                }
            }
            else
            {
                _logger.Add("Disabling autostart...");
                var success = await _autoStartService.DisableAutoStartAsync();
                if (!success)
                {
                    await ShowToastOrAlertAsync("Warning: AutoStart disable may have failed. Check logs for details.", true);
                }
            }
            */

            // Refresh status without trying to apply changes
            await RefreshAutoStartStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.Add($"Error saving autostart settings: {ex.Message}");
            await ShowToastOrAlertAsync($"AutoStart configuration error: {ex.Message}", true);
        }
    }

    private async Task ReapplyWallpapersIfNeededAsync(WallpaperFitMode oldFitMode, WallpaperFitMode newFitMode)
    {
        try
        {
            if (oldFitMode != newFitMode)
            {
                _logger.Add($"Wallpaper fit mode changed from {oldFitMode} to {newFitMode}, reapplying wallpapers...");
                
                var wallpaperManagement = MauiProgram.Services?.GetService<WallpaperManagementService>();
                if (wallpaperManagement != null)
                {
                    var success = await wallpaperManagement.ReapplyCurrentWallpapersAsync();
                    if (success)
                    {
                        await ShowToastOrAlertAsync("Wallpapers reapplied with new fit mode!");
                    }
                    else
                    {
                        _logger.Add("No current wallpapers to reapply or reapplication failed.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Error reapplying wallpapers: {ex.Message}");
        }
    }

    private async void OnSelectPanicFileClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            // Check storage permissions using the comprehensive helper
            var hasStoragePermission = await AndroidStorageHelper.CheckAndRequestStoragePermissionAsync();
            if (!hasStoragePermission)
            {
                await ShowToastOrAlertAsync("Storage permission is required to save files to the AnonClient directory.", true);
                return;
            }

            // Also request media permission for file selection
            var mediaStatus = await Permissions.RequestAsync<ReadMediaImagesPermission>();
            if (mediaStatus != PermissionStatus.Granted)
            {
                await ShowToastOrAlertAsync("Media permission is required to select a local file.", true);
                return;
            }
#endif

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Panic Wallpaper",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
#if ANDROID
                // On Android, copy to internal storage AnonClient directory
                var externalStorageDir = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
                var anonClientDir = !string.IsNullOrEmpty(externalStorageDir) 
                    ? Path.Combine(externalStorageDir, "AnonClient", "settings")
                    : Path.Combine(FileSystem.AppDataDirectory, "settings");
                
                _logger.Add($"Creating directory: {anonClientDir}");
                Directory.CreateDirectory(anonClientDir);
                
                var newPath = Path.Combine(anonClientDir, "panic_wallpaper.jpg");
                _logger.Add($"Saving panic file to: {newPath}");
                
                using (var stream = await result.OpenReadAsync())
                using (var newStream = File.OpenWrite(newPath))
                {
                    await stream.CopyToAsync(newStream);
                }
                _settingsService.SetPanicFilePath(newPath);
#else
                // On Windows and other platforms, use the original file path
                _settingsService.SetPanicFilePath(result.FullPath);
#endif
                PanicFileLabel.Text = "Local file set!";
                _logger.Add($"Panic wallpaper set to: {result.FullPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Error in panic file selection: {ex.Message}");
            await DisplayAlert("Error", $"Failed to select or save panic file: {ex.Message}", "OK");
        }
    }

    private async void OnExportDataClicked(object sender, EventArgs e)
    {
        try
        {
            var targetFile = Path.Combine(FileSystem.CacheDirectory, $"AnonWallClient_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            var success = await _settingsService.ExportDataAsync(targetFile);
            if (success)
            {
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Export AnonWallClient Data",
                    File = new ShareFile(targetFile)
                });
            }
            else
            {
                await DisplayAlert("Error", "Failed to export data.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to export data: {ex.Message}", "OK");
        }
    }

    private async void OnImportDataClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Import AnonWallClient Data",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.zip-archive" } },
                    { DevicePlatform.Android, new[] { "application/zip" } },
                    { DevicePlatform.WinUI, new[] { ".zip" } },
                    { DevicePlatform.macOS, new[] { "public.zip-archive" } },
                })
            });

            if (result != null)
            {
                var success = await _settingsService.ImportDataAsync(result.FullPath);
                if (success)
                {
                    LoadSettings(); // Reload UI
                    await DisplayAlert("Success", "Data imported successfully. Please restart the app for all changes to take effect.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to import data. Please ensure the file is a valid AnonWallClient backup.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to import data: {ex.Message}", "OK");
        }
    }

    private async void OnOpenDataFolderClicked(object sender, EventArgs e)
    {
        try
        {
            var path = _settingsService.GetDataFolderPath();
#if WINDOWS
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
#else
            await DisplayAlert("Data Folder", $"Data is stored in: {path}", "OK");
#endif
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open folder: {ex.Message}", "OK");
        }
    }

    private async void OnSelectSaveFolderClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            await DisplayAlert("Info", "On Android, wallpapers are saved to /Internal storage/AnonClient/pictures/ by default. This cannot be changed.", "OK");
#elif WINDOWS
            // Use FilePicker to select a folder (workaround since FolderPicker doesn't exist in MAUI)
            await DisplayAlert("Info", "On Windows, wallpapers are saved to Documents/AnonClient/pictures/ by default. To change this, you can manually set the folder path in the app data files.", "OK");
#else
            await DisplayAlert("Info", "Folder selection is not available on this platform. Using default Documents/AnonClient/pictures/ folder.", "OK");
#endif
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select folder: {ex.Message}", "OK");
        }
    }

    private static bool _isImmediateTriggerInProgress = false;

    private async Task TriggerImmediateWallpaperSettingAsync()
    {
        try
        {
            if (_isImmediateTriggerInProgress) return;
            
            _isImmediateTriggerInProgress = true;

            var wallpaperLinkId = _settingsService.GetWallpaperLinkId();
            var lockscreenLinkId = _settingsService.GetLockscreenLinkId();
            var apiKey = _settingsService.GetApiKey();
            var linkIdMode = _settingsService.GetLinkIdMode();

            // Only trigger if we have valid configuration
            if (string.IsNullOrEmpty(apiKey)) 
            {
                _isImmediateTriggerInProgress = false;
                return;
            }

            _logger.Add("SettingsPage: Triggering immediate wallpaper setting after save...");

            // Get the walltaker service and wallpaper service
            var walltakerService = MauiProgram.Services?.GetService<WalltakerService>();
            var wallpaperService = MauiProgram.Services?.GetService<IWallpaperService>();
            
            if (walltakerService != null && wallpaperService != null)
            {
                // Add a small delay to let settings save complete
                await Task.Delay(1000);
                
                _ = Task.Run(async () => 
                {
                    try
                    {
                        // Check for new wallpapers for both types
                        var newWallpapers = await walltakerService.CheckMultipleLinkIdsAsync(
                            wallpaperLinkId, 
                            lockscreenLinkId, 
                            linkIdMode);

                        foreach (var (imageUrl, wallpaperType) in newWallpapers)
                        {
                            _logger.Add($"SettingsPage: Setting {wallpaperType} from immediate trigger: {imageUrl}");
                            
                            var fitMode = _settingsService.GetWallpaperFitMode();
                            var success = await wallpaperService.SetWallpaperAsync(imageUrl, fitMode, wallpaperType);
                            
                            if (success)
                            {
                                _logger.Add($"SettingsPage: {wallpaperType} set successfully from settings save trigger.");
                            }
                            else
                            {
                                _logger.Add($"SettingsPage: Failed to set {wallpaperType} from settings save trigger.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Add($"SettingsPage: Error in immediate wallpaper setting: {ex.Message}");
                    }
                    finally
                    {
                        // Reset the flag after operation completes
                        await Task.Delay(3000);
                        _isImmediateTriggerInProgress = false;
                    }
                });
            }
            else
            {
                _isImmediateTriggerInProgress = false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"SettingsPage: Error triggering immediate wallpaper setting: {ex.Message}");
            _isImmediateTriggerInProgress = false;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if ANDROID
        // Request storage permissions on Android for external storage access
        await RequestStoragePermissionsAsync();
#endif

        // Refresh cache info and autostart status when page appears
        UpdateCacheInfo();
        await RefreshAutoStartStatusAsync();
    }

#if ANDROID
    private async Task RequestStoragePermissionsAsync()
    {
        try
        {
            _logger.Add("SettingsPage: Checking storage permissions...");
            
            var hasPermission = await AndroidStorageHelper.CheckAndRequestStoragePermissionAsync();
            
            if (hasPermission)
            {
                _logger.Add("SettingsPage: Storage permissions granted.");
            }
            else
            {
                _logger.Add("SettingsPage: Storage permissions not granted.");
                
                // For Android 11+, show specific message about MANAGE_EXTERNAL_STORAGE
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
                {
                    await ShowToastOrAlertAsync("For full functionality, please enable 'Allow access to manage all files' in the app settings that just opened.", true);
                }
                else
                {
                    await ShowToastOrAlertAsync("Storage permissions are required for the app to function properly. Some features may not work.", true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"SettingsPage: Error requesting storage permissions: {ex.Message}");
        }
    }
#endif
}
