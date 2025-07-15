using AnonWallClient.Services;
using AnonWallClient.Background;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Collections.Generic;

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

    public SettingsPage(PollingService pollingService, AppLogService logger)
    {
        InitializeComponent();
        _pollingService = pollingService;
        _logger = logger;
        LoadSettings();
    }

    private void LoadSettings()
    {
        LinkIdEntry.Text = Preferences.Get("link_id", string.Empty);
        ApiKeyEntry.Text = Preferences.Get("api_key", string.Empty);
        PanicUrlEntry.Text = Preferences.Get("panic_url", string.Empty);
        var savedPanicFile = Preferences.Get("panic_file_path", string.Empty);
        PanicFileLabel.Text = string.IsNullOrEmpty(savedPanicFile) ? "No local file selected." : "Local file set!";
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        Preferences.Set("link_id", LinkIdEntry.Text);
        Preferences.Set("api_key", ApiKeyEntry.Text);
        Preferences.Set("panic_url", PanicUrlEntry.Text);

        _pollingService.EnablePolling();

        await MainThread.InvokeOnMainThreadAsync(() => Toast.Make("Settings Saved! Polling is enabled.", ToastDuration.Short).Show());

        await Shell.Current.GoToAsync("//HomePage");
    }

    private async void OnSelectPanicFileClicked(object sender, EventArgs e)
    {
#if ANDROID
        var status = await Permissions.RequestAsync<ReadMediaImagesPermission>();

        if (status == PermissionStatus.Granted)
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Panic Wallpaper",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                var newPath = Path.Combine(FileSystem.AppDataDirectory, "panic_wallpaper.jpg");
                using (var stream = await result.OpenReadAsync())
                using (var newStream = File.OpenWrite(newPath))
                {
                    await stream.CopyToAsync(newStream);
                }

                Preferences.Set("panic_file_path", newPath);
                PanicFileLabel.Text = "Local file set!";
                _logger.Add($"Panic wallpaper set to internal path: {newPath}");
            }
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make("Storage permission is required to select a local file.", ToastDuration.Long).Show());
        }
#else
        await DisplayAlert("Not Supported", "Selecting a local file is only supported on Windows in this version.", "OK");
#endif
    }
}