using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AnonWallClient.Models;

namespace AnonWallClient.Services;

public class WalltakerService(IHttpClientFactory httpClientFactory, WallpaperHistoryService historyService)
{
    private string? _lastImageUrl = null;
    private readonly WallpaperHistoryService _historyService = historyService;

    public async Task<string?> GetNewWallpaperUrlAsync(string linkId)
    {
        if (string.IsNullOrWhiteSpace(linkId))
        {
            return null;
        }

        var httpClient = httpClientFactory.CreateClient("WalltakerClient");
        var url = $"https://walltaker.joi.how/api/links/{linkId}.json";

        try
        {
            var response = await httpClient.GetFromJsonAsync<LinkData>(url);
            var newImageUrl = response?.PostUrl;

            if (!string.IsNullOrEmpty(newImageUrl))
            {
                // Initialize _lastImageUrl from history if not set (app startup)
                if (_lastImageUrl == null && _historyService.History.Any())
                {
                    _lastImageUrl = _historyService.History.First().ImageUrl;
                }

                // Check if this is truly a new wallpaper
                if (newImageUrl != _lastImageUrl)
                {
                    _lastImageUrl = newImageUrl;
                    return newImageUrl;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
        }

        return null;
    }

    public async Task<(bool Success, string ErrorMessage)> PostResponseAsync(string linkId, string apiKey, string responseType, string? responseText = null)
    {
        if (string.IsNullOrWhiteSpace(linkId) || string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "Link ID or API Key is missing.");
        }

        var httpClient = httpClientFactory.CreateClient("WalltakerClient");
        var url = $"https://walltaker.joi.how/api/links/{linkId}/response.json";

        var payload = new ResponseData
        {
            ApiKey = apiKey,
            ResponseType = responseType,
            Text = responseText
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync(url, jsonContent);
            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return (false, $"Server returned error: {response.StatusCode}. Details: {errorBody}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }
}