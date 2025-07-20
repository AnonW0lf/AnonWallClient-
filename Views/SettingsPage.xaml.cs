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
#endif

public partial class SettingsPage : ContentPage
{
    private readonly PollingService _pollingService;
    private readonly AppLogService _logger;
    private readonly SettingsService _settingsService;

    public SettingsPage(PollingService pollingService, AppLogService logger, SettingsService settingsService)
    {
        InitializeComponent();
        _pollingService = pollingService;
        _logger = logger;
        _settingsService = settingsService;
        LoadSettings();
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

    private void LoadSettings()
    {
        try
        {
            LinkIdEntry.Text = _settingsService.GetLinkId();
            ApiKeyEntry.Text = _settingsService.GetApiKey();
            PanicUrlEntry.Text = _settingsService.GetPanicUrl();
            var savedPanicFile = _settingsService.GetPanicFilePath();
            PanicFileLabel.Text = string.IsNullOrEmpty(savedPanicFile) ? "No local file selected." : "Local file set!";
            PollingIntervalEntry.Text = _settingsService.GetPollingIntervalSeconds().ToString();
            WifiOnlyCheckBox.IsChecked = _settingsService.GetWifiOnly();
            MaxHistoryEntry.Text = _settingsService.GetMaxHistoryLimit().ToString();
            
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

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            _settingsService.SetLinkId(LinkIdEntry.Text);
            _settingsService.SetApiKey(ApiKeyEntry.Text);
            _settingsService.SetPanicUrl(PanicUrlEntry.Text);
            
            if (int.TryParse(PollingIntervalEntry.Text, out int interval) && interval > 0)
            {
                _settingsService.SetPollingIntervalSeconds(interval);
            }
            else
            {
                await DisplayAlert("Invalid Input", "Polling interval must be a positive number.", "OK");
                return;
            }
            
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
            
            _settingsService.SetWifiOnly(WifiOnlyCheckBox.IsChecked);

            _pollingService.EnablePolling();

            await ShowToastOrAlertAsync("Settings Saved! Polling is enabled.");

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

    private async void OnSelectPanicFileClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var status = await Permissions.RequestAsync<ReadMediaImagesPermission>();
            if (status != PermissionStatus.Granted)
            {
                await ShowToastOrAlertAsync("Storage permission is required to select a local file.", true);
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
                // On Android, copy to app data directory
                var newPath = Path.Combine(FileSystem.AppDataDirectory, "panic_wallpaper.jpg");
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
            await DisplayAlert("Info", "On Android, wallpapers are saved to Pictures/AnonWallClient by default. This cannot be changed.", "OK");
#elif WINDOWS
            // Use FilePicker to select a folder (workaround since FolderPicker doesn't exist in MAUI)
            await DisplayAlert("Info", "On Windows, wallpapers are saved to Pictures/AnonWallClient by default. To change this, you can manually set the folder path in the app data files.", "OK");
#else
            await DisplayAlert("Info", "Folder selection is not available on this platform. Using default Pictures/AnonWallClient folder.", "OK");
#endif
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select folder: {ex.Message}", "OK");
        }
    }
}