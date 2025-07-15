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
    private bool _isServiceStarted = false;

    public HomePage(AppLogService logger, PollingService pollingService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _logger = logger;
        _pollingService = pollingService;
        _serviceProvider = serviceProvider;

        _logger.Logs.CollectionChanged += OnLogsCollectionChanged;
        LogEditor.Text = string.Join(Environment.NewLine, _logger.Logs);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isServiceStarted)
        {
            _isServiceStarted = true;

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

            var savedLinkId = Preferences.Get("link_id", string.Empty);
            if (!string.IsNullOrEmpty(savedLinkId))
            {
                _pollingService.EnablePolling();
            }
        }
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
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make("ERROR: Link ID and API Key must be set.", ToastDuration.Long).Show());
            return;
        }

        var (isSuccess, errorMessage) = await _pollingService.PostResponseAsync(linkId, apiKey, responseType);

        if (isSuccess)
        {
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make("Response Sent!", ToastDuration.Short).Show());
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make($"Failed: {errorMessage}", ToastDuration.Long).Show());
        }
    }

    private async void OnCopyLogClicked(object sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(LogEditor.Text);
    }

    private void OnPanicClicked(object sender, EventArgs e)
    {
        _logger.Add("UI Panic button clicked.");
        var panicPath = Preferences.Get("panic_file_path", string.Empty);
        if (string.IsNullOrEmpty(panicPath)) panicPath = Preferences.Get("panic_url", string.Empty);

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
        var serviceManager = _serviceProvider.GetService<IForegroundServiceManager>();
        serviceManager?.StopService();
#endif
        Application.Current?.Quit();
    }
}