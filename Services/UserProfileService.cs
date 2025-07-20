using System.Text.Json;
using AnonWallClient.Models;

namespace AnonWallClient.Services;

public class UserProfileService
{
    private readonly HttpClient _httpClient;
    private readonly AppLogService _logger;
    private readonly SettingsService _settingsService;
    private readonly HtmlProfileParserService _htmlParser;
    private UserProfile? _cachedProfile;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5); // Cache for 5 minutes

    public UserProfileService(IHttpClientFactory httpClientFactory, AppLogService logger, SettingsService settingsService, HtmlProfileParserService htmlParser)
    {
        _httpClient = httpClientFactory.CreateClient("WalltakerClient");
        _logger = logger;
        _settingsService = settingsService;
        _htmlParser = htmlParser;
    }

    public async Task<UserProfile?> GetUserProfileAsync(string? username = null, bool forceRefresh = false)
    {
        try
        {
            var apiKey = _settingsService.GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.Add("UserProfile: No API key configured");
                return null;
            }

            // Use cached profile if available and not expired
            if (!forceRefresh && _cachedProfile != null && 
                DateTime.Now - _lastFetch < _cacheTimeout)
            {
                _logger.Add("UserProfile: Using cached profile data");
                return _cachedProfile;
            }

            // If no username provided, try to get it from existing profile or links
            if (string.IsNullOrWhiteSpace(username))
            {
                username = await GetUsernameFromLinksAsync();
                if (string.IsNullOrWhiteSpace(username))
                {
                    _logger.Add("UserProfile: No username available for profile lookup");
                    return null;
                }
            }

            _logger.Add($"UserProfile: Fetching profile for user: {username}");

            // Fetch JSON API data
            var profile = await FetchJsonProfileAsync(username, apiKey);
            if (profile == null)
            {
                return null;
            }

            // Fetch and parse HTML data
            try
            {
                var htmlProfile = await FetchHtmlProfileAsync(username);
                if (htmlProfile != null)
                {
                    // Merge HTML data into JSON profile
                    profile = htmlProfile;
                    _logger.Add("UserProfile: Successfully merged HTML and JSON profile data");
                }
                else
                {
                    _logger.Add("UserProfile: Using JSON data only (HTML parsing failed)");
                }
            }
            catch (Exception ex)
            {
                _logger.Add($"UserProfile: HTML parsing failed, using JSON only: {ex.Message}");
            }

            _cachedProfile = profile;
            _lastFetch = DateTime.Now;
            return profile;
        }
        catch (Exception ex)
        {
            _logger.Add($"UserProfile: Error fetching user profile: {ex.Message}");
            return null;
        }
    }

    private async Task<UserProfile?> FetchJsonProfileAsync(string username, string apiKey)
    {
        try
        {
            var apiUrl = $"https://walltaker.joi.how/api/users/{username}.json?api_key={apiKey}";
            var response = await _httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var profile = JsonSerializer.Deserialize<UserProfile>(json);

                if (profile != null)
                {
                    _logger.Add($"UserProfile: Successfully fetched JSON profile for {profile.Username}");
                    return profile;
                }
            }
            else
            {
                _logger.Add($"UserProfile: JSON API request failed with status: {response.StatusCode}");
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Add($"UserProfile: Error fetching JSON profile: {ex.Message}");
            return null;
        }
    }

    private async Task<UserProfile?> FetchHtmlProfileAsync(string username)
    {
        try
        {
            _logger.Add($"UserProfile: Fetching HTML profile for {username}");
            
            var htmlUrl = $"https://walltaker.joi.how/users/{username}";
            var response = await _httpClient.GetAsync(htmlUrl);

            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync();
                
                // First get the JSON profile for the base data
                var apiKey = _settingsService.GetApiKey();
                var jsonProfile = await FetchJsonProfileAsync(username, apiKey!);
                
                // Parse HTML and merge with JSON data
                var enhancedProfile = await _htmlParser.ParseProfileHtmlAsync(html, jsonProfile);
                
                if (enhancedProfile != null)
                {
                    _logger.Add("UserProfile: Successfully fetched and parsed HTML profile");
                    return enhancedProfile;
                }
            }
            else
            {
                _logger.Add($"UserProfile: HTML request failed with status: {response.StatusCode}");
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Add($"UserProfile: Error fetching HTML profile: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> GetUsernameFromLinksAsync()
    {
        try
        {
            var apiKey = _settingsService.GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            // Try to get username from wallpaper LinkID
            var linkId = _settingsService.GetWallpaperLinkId();
            if (string.IsNullOrWhiteSpace(linkId))
                linkId = _settingsService.GetLinkId(); // Fallback to legacy LinkID

            if (!string.IsNullOrWhiteSpace(linkId))
            {
                var apiUrl = $"https://walltaker.joi.how/api/links/{linkId}.json";
                var response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("username", out var usernameProp))
                    {
                        var username = usernameProp.GetString();
                        _logger.Add($"UserProfile: Found username '{username}' from LinkID {linkId}");
                        return username;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Add($"UserProfile: Error getting username from links: {ex.Message}");
            return null;
        }
    }

    public void ClearCache()
    {
        _cachedProfile = null;
        _lastFetch = DateTime.MinValue;
        _logger.Add("UserProfile: Cache cleared");
    }

    public bool IsProfileAvailable()
    {
        var apiKey = _settingsService.GetApiKey();
        return !string.IsNullOrWhiteSpace(apiKey);
    }
}
