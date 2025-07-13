using AnonWallClient.Services;

namespace AnonWallClient.Views;

public partial class SettingsPage : ContentPage
{
    private readonly PollingService _pollingService;

    public SettingsPage(PollingService pollingService)
    {
        InitializeComponent();
        _pollingService = pollingService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        LinkIdEntry.Text = Preferences.Get("link_id", string.Empty);
        ApiKeyEntry.Text = Preferences.Get("api_key", string.Empty);
        PanicUrlEntry.Text = Preferences.Get("panic_url", string.Empty);
        var savedPanicFile = Preferences.Get("panic_file_path", string.Empty);
        PanicFileLabel.Text = string.IsNullOrEmpty(savedPanicFile) ? "No local file selected." : savedPanicFile;
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        Preferences.Set("link_id", LinkIdEntry.Text);
        Preferences.Set("api_key", ApiKeyEntry.Text);
        Preferences.Set("panic_url", PanicUrlEntry.Text);
        
        // The panic_file_path is saved in the OnSelectPanicFileClicked method
        
        _pollingService.EnablePolling();
        Toast.Make("Settings Saved! Polling is enabled.").Show();
    }

    private async void OnSelectPanicFileClicked(object sender, EventArgs e)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.StorageRead>();
        }

        if (status == PermissionStatus.Granted)
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Panic Wallpaper",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                Preferences.Set("panic_file_path", result.FullPath);
                PanicFileLabel.Text = result.FullPath;
            }
        }
        else
        {
            await Toast.Make("Storage permission not granted.").Show();
        }
    }
}