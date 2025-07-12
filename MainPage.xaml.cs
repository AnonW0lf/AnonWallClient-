using AnonWallClient.Services;
using System.Collections.Specialized;
using AnonWallClient.Background;
using Microsoft.Extensions.DependencyInjection;

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

            // Add a small delay to ensure the UI thread is fully initialized before proceeding.
            await Task.Delay(250);

#if ANDROID
            // Request notification permission directly on the UI thread
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

            // Start the C# polling task *after* handling platform-specific services
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
        if (!string.IsNullOrEmpty(savedLinkId))
        {
            StatusLabel.Text = "Service is running in the background.";
        }
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        var linkId = LinkIdEntry.Text;
        if (!string.IsNullOrWhiteSpace(linkId))
        {
            Preferences.Set("link_id", linkId);
            StatusLabel.Text = "Settings saved! Polling enabled.";
            _logger.Add($"Link ID set to: {LinkIdEntry.Text}.");
            _pollingService.EnablePolling();
        }
        else
        {
            StatusLabel.Text = "Please enter a valid Link ID.";
        }
    }

    private async void OnCopyLogClicked(object sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(LogEditor.Text);
        _logger.Add("Log copied to clipboard.");
    }
}