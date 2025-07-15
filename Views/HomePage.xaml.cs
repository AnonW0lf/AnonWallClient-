using AnonWallClient.Services;
using System.Collections.Specialized;
using AnonWallClient.Background;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace AnonWallClient.Views;

public partial class HomePage : ContentPage
{
    private readonly AppLogService _logger;
    private readonly PollingService _pollingService;

    public HomePage(AppLogService logger, PollingService pollingService)
    {
        InitializeComponent();
        _logger = logger;
        _pollingService = pollingService;

        _logger.Logs.CollectionChanged += OnLogsCollectionChanged;
        LogEditor.Text = string.Join(Environment.NewLine, _logger.Logs);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // This logic is now handled in the AndroidForegroundService and AppShell
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogEditor.Text = string.Join(Environment.NewLine, _logger.Logs);
        });
    }

    private async void OnHornyClicked(object sender, EventArgs e) => await SendResponse("horny");
    private async void OnDisgustClicked(object sender, EventArgs e) => await SendResponse("disgust");
    private async void OnCameClicked(object sender, EventArgs e) => await SendResponse("came");

    private async Task SendResponse(string responseType)
    {
        var linkId = Preferences.Get("link_id", string.Empty);
        var apiKey = Preferences.Get("api_key", string.Empty);

        if (string.IsNullOrWhiteSpace(linkId) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.Add("ERROR: Link ID and API Key must be set to send a response.");
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make("ERROR: Link ID and API Key must be set.", ToastDuration.Long).Show());
            return;
        }

        _logger.Add($"Sending '{responseType}' response...");
        var (isSuccess, errorMessage) = await _pollingService.PostResponseAsync(linkId, apiKey, responseType);

        if (isSuccess)
        {
            _logger.Add("Response sent successfully!");
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make("Response Sent!", ToastDuration.Short).Show());
        }
        else
        {
            _logger.Add($"Failed to send response: {errorMessage}");
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make($"Failed: {errorMessage}", ToastDuration.Long).Show());
        }
    }

    private async void OnCopyLogClicked(object sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(LogEditor.Text);
        _logger.Add("Log copied to clipboard.");
    }

    // --- ADDING THESE TWO MISSING METHODS ---
    private void OnPanicClicked(object sender, EventArgs e)
    {
        _logger.Add("UI Panic button clicked.");
        var panicPath = Preferences.Get("panic_file_path", string.Empty);
        if (string.IsNullOrEmpty(panicPath))
        {
            panicPath = Preferences.Get("panic_url", string.Empty);
        }

        if (!string.IsNullOrEmpty(panicPath) && MauiProgram.Services is not null)
        {
            var wallpaperService = MauiProgram.Services.GetService<IWallpaperService>();
            _ = wallpaperService?.SetWallpaperAsync(panicPath);
        }

        OnExitClicked(sender, e);
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        _logger.Add("UI Exit button clicked.");
#if ANDROID
        var serviceManager = this.Handler!.MauiContext!.Services.GetService<IForegroundServiceManager>();
        serviceManager?.StopService();
#endif
        Application.Current?.Quit();
    }
}