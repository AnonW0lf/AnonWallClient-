using AnonWallClient.Services;
using System.Collections.Specialized;
using AnonWallClient.Background;
using Microsoft.Extensions.DependencyInjection;

namespace AnonWallClient;

public partial class MainPage : ContentPage
{
    private readonly AppLogService _logger;
    private readonly PollingService _pollingService;

    public MainPage(AppLogService logger, PollingService pollingService)
    {
        InitializeComponent();
        _logger = logger;
        _pollingService = pollingService;
        LoadSettings();

        _logger.Logs.CollectionChanged += OnLogsCollectionChanged;
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

            // This is now the ONLY place where polling is activated.
            _pollingService.EnablePolling();
        }
        else
        {
            StatusLabel.Text = "Please enter a valid Link ID.";
        }
    }
}