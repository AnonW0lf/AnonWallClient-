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
    private readonly WallpaperHistoryService _historyService;
    private readonly SettingsService _settingsService;
    private bool _isServiceStarted = false;

    public HomePage(AppLogService logger, PollingService pollingService, IServiceProvider serviceProvider, SettingsService settingsService)
    {
        InitializeComponent();
        _logger = logger;
        _pollingService = pollingService;
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _historyService = MauiProgram.Services?.GetService<WallpaperHistoryService>()!;

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
            var current = _historyService?.History?.FirstOrDefault();
            if (current != null)
            {
                CurrentWallpaperImage.Source = current.ThumbnailUrl ?? current.ImageUrl;
                CurrentWallpaperDescription.Text = current.Description ?? "No description";
                CurrentWallpaperSetTime.Text = $"Set: {current.SetTime:g}";
            }
            else
            {
                CurrentWallpaperImage.Source = null;
                CurrentWallpaperDescription.Text = "No wallpaper set yet.";
                CurrentWallpaperSetTime.Text = string.Empty;
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
            await Clipboard.SetTextAsync(LogEditor.Text);
            await ShowToastOrAlertAsync("Log copied to clipboard.");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Clipboard Error", $"Failed to copy log: {ex.Message}", "OK");
        }
    }

    private void OnPanicClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.Add("UI Panic button clicked.");
            var panicPath = _settingsService.GetPanicFilePath();
            if (string.IsNullOrEmpty(panicPath)) 
                panicPath = _settingsService.GetPanicUrl();

            if (!string.IsNullOrEmpty(panicPath) && MauiProgram.Services is not null)
            {
                var wallpaperService = MauiProgram.Services.GetService<IWallpaperService>();
                try { _ = wallpaperService?.SetWallpaperAsync(panicPath); } catch { }
            }
            OnExitClicked(sender, e);
        }
        catch (Exception ex)
        {
            _logger.Add($"Panic error: {ex.Message}");
            DisplayAlert("Panic Error", $"{ex.Message}", "OK");
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
                
                // If it's a disgust response, suggest re-polling for wallpaper rollback
                if (responseType == "disgust")
                {
                    _logger.Add("Disgust response sent - wallpaper should be rolled back on next poll.");
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