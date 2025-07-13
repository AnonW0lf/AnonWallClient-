using AnonWallClient.Services;
using System.Collections.Specialized;
using AnonWallClient.Background;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace AnonWallClient;

public partial class MainPage : ContentPage
{
    private readonly AppLogService _logger;
    private readonly PollingService _pollingService;
    private readonly IServiceProvider _serviceProvider;
    private bool _isServiceStarted = false;

    public MainPage(AppLogService logger, PollingService pollingService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _logger = logger;
        _pollingService = pollingService;
        _serviceProvider = serviceProvider;
        LoadSettings();

        _logger.Logs.CollectionChanged += OnLogsCollectionChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isServiceStarted)
        {
            _isServiceStarted = true;
            await Task.Delay(250);

#if ANDROID
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                _logger.Add("Requesting notification permission...");
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            }

            if (status == PermissionStatus.Granted)
            {
                _logger.Add("Notification permission granted. Starting foreground service.");
                var serviceManager = _serviceProvider.GetService<IForegroundServiceManager>();
                serviceManager?.StartService();
            }
            else
            {
                _logger.Add("Notification permission denied. Background service may not be reliable.");
            }
#endif

            _ = Task.Run(() => _pollingService.StartPollingAsync(new CancellationToken()));
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogEditor.Text = string.Join(Environment.NewLine, _logger.Logs);
        });
    }

    private void LoadSettings()
    {
        var savedLinkId = Preferences.Get("link_id", string.Empty);
        LinkIdEntry.Text = savedLinkId;
        var savedApiKey = Preferences.Get("api_key", string.Empty);
        ApiKeyEntry.Text = savedApiKey;

        if (!string.IsNullOrEmpty(savedLinkId))
        {
            StatusLabel.Text = "Service is running in the background.";
        }
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        var linkId = LinkIdEntry.Text;
        var apiKey = ApiKeyEntry.Text;

        if (!string.IsNullOrWhiteSpace(linkId))
        {
            Preferences.Set("link_id", linkId);
            Preferences.Set("api_key", apiKey);
            StatusLabel.Text = "Settings saved! Polling enabled.";
            _logger.Add($"Link ID set to: {LinkIdEntry.Text}.");
            _pollingService.EnablePolling();
        }
        else
        {
            StatusLabel.Text = "Please enter a valid Link ID.";
        }
    }

    private async void OnHornyClicked(object sender, EventArgs e) => await SendResponse("horny");
    private async void OnDisgustClicked(object sender, EventArgs e) => await SendResponse("disgust");
    private async void OnCameClicked(object sender, EventArgs e) => await SendResponse("came");

    private async Task SendResponse(string responseType)
    {
        var linkId = Preferences.Get("link_id", string.Empty);
        var apiKey = ApiKeyEntry.Text;

        if (string.IsNullOrWhiteSpace(linkId) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.Add("ERROR: Link ID and API Key must be set to send a response.");
            #if ANDROID
                await Toast.Make("ERROR: Link ID and API Key must be set.", ToastDuration.Long).Show();
            #endif
            return;
        }

        _logger.Add($"Sending '{responseType}' response...");
        var result = await _pollingService.PostResponseAsync(linkId, apiKey, responseType);

        if (result.Success)
        {
            _logger.Add("Response sent successfully!");
#if ANDROID
                await Toast.Make("Response Sent!").Show()
#endif
        }
        else
        {
            _logger.Add($"Failed to send response: {result.ErrorMessage}");
            #if ANDROID
                await Toast.Make($"Failed: {result.ErrorMessage}", ToastDuration.Long).Show();
            #endif
        }
    }

    private async void OnCopyLogClicked(object sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(LogEditor.Text);
        _logger.Add("Log copied to clipboard.");
    }
}