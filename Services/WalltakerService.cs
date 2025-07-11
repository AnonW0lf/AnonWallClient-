using System.Net.Http;
using System.Net.Http.Json;
using AnonWallClient.Models;

namespace AnonWallClient.Services;

// Use primary constructor for cleaner code
public class WalltakerService(IHttpClientFactory httpClientFactory)
{
    private string? _lastImageUrl = null;

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

            if (!string.IsNullOrEmpty(newImageUrl) && newImageUrl != _lastImageUrl)
            {
                _lastImageUrl = newImageUrl;
                return newImageUrl;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error: {ex.Message}");
        }

        return null;
    }
}