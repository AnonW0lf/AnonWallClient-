using AnonWallClient.Services;
using System.Collections.Specialized;

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

        LogEditor.ItemsSource = _logger.Logs;
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
            await Toast.Make("ERROR: Link ID and API Key must be set.", ToastDuration.Long).Show();
            return;
        }

        _logger.Add($"Sending '{responseType}' response...");
        var result = await _pollingService.PostResponseAsync(linkId, apiKey, responseType);

        if (result.Success)
        {
            _logger.Add("Response sent successfully!");
            await Toast.Make("Response Sent!").Show();
        }
        else
        {
            _logger.Add($"Failed to send response: {result.ErrorMessage}");
            await Toast.Make($"Failed: {result.ErrorMessage}", ToastDuration.Long).Show();
        }
    }

    private async void OnCopyLogClicked(object sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(LogEditor.Text);
        _logger.Add("Log copied to clipboard.");
    }
}