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
    private bool _isPollingStarted = false;

    public MainPage(AppLogService logger, PollingService pollingService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _logger = logger;
        _pollingService = pollingService;
        _serviceProvider = serviceProvider;
        LoadSettings();

        _logger.Logs.CollectionChanged += OnLogsCollectionChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_isPollingStarted)
        {
#if ANDROID
            _ = Task.Run(async () => await StartAndroidServices());
#else
            _isPollingStarted = true;
            _ = Task.Run(() => _pollingService.StartPollingAsync(new CancellationToken()));
#endif
        }
    }

#if ANDROID
    private async Task StartAndroidServices()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        _logger.Add($"Initial notification permission status: {status}");

        if (status != PermissionStatus.Granted)
        {
            _logger.Add("Requesting notification permission...");
            status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            _logger.Add($"Result of permission request: {status}");
        }

        if (status == PermissionStatus.Granted)
        {
            _logger.Add("Notification permission granted.");
            var serviceManager = _serviceProvider.GetService<IForegroundServiceManager>();
            serviceManager?.StartService();
        }
        else
        {
            _logger.Add("Notification permission was not granted. Background service may not be reliable.");
        }
        
        _isPollingStarted = true;
        _ = Task.Run(() => _pollingService.StartPollingAsync(new CancellationToken()));
    }
#endif

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