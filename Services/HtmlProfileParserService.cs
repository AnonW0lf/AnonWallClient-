using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Globalization;
using AnonWallClient.Models;

namespace AnonWallClient.Services;

public class HtmlProfileParserService
{
    private readonly AppLogService _logger;

    public HtmlProfileParserService(AppLogService logger)
    {
        _logger = logger;
    }

    public async Task<UserProfile?> ParseProfileHtmlAsync(string html, UserProfile? existingProfile = null)
    {
        try
        {
            var profile = existingProfile ?? new UserProfile();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            _logger.Add("HtmlParser: Starting HTML profile parsing...");

            // Extract profile description
            ExtractProfileDescription(doc, profile);

            // Extract activity stats
            ExtractActivityStats(doc, profile);

            // Extract last online information
            ExtractLastOnlineInfo(doc, profile);

            // Extract recent wallpapers
            ExtractRecentWallpapers(doc, profile);

            // Extract activity chart data
            ExtractActivityChart(doc, profile);

            // Extract enhanced link information
            ExtractEnhancedLinkInfo(doc, profile);

            profile.HasHtmlData = true;
            _logger.Add("HtmlParser: Successfully parsed HTML profile data");

            return profile;
        }
        catch (Exception ex)
        {
            _logger.Add($"HtmlParser: Error parsing HTML profile: {ex.Message}");
            return existingProfile;
        }
    }

    private void ExtractProfileDescription(HtmlDocument doc, UserProfile profile)
    {
        try
        {
            var descriptionNode = doc.DocumentNode
                .SelectSingleNode("//div[@class='details__container']//blockquote");

            if (descriptionNode != null)
            {
                var description = descriptionNode.InnerText?.Trim();
                if (!string.IsNullOrEmpty(description) && 
                    !description.Contains("No profile description"))
                {
                    profile.ProfileDescription = description;
                    _logger.Add($"HtmlParser: Found profile description: {description.Substring(0, Math.Min(50, description.Length))}...");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"HtmlParser: Error extracting profile description: {ex.Message}");
        }
    }

    private void ExtractActivityStats(HtmlDocument doc, UserProfile profile)
    {
        try
        {
            var statsNodes = doc.DocumentNode
                .SelectNodes("//div[@class='gios-user-grid']//small");

            if (statsNodes != null)
            {
                foreach (var node in statsNodes)
                {
                    var text = node.InnerText;
                    
                    // Extract "Set X wallpapers"
                    var setMatch = Regex.Match(text, @"Set (\d+) wallpapers");
                    if (setMatch.Success && int.TryParse(setMatch.Groups[1].Value, out int wallpapers))
                    {
                        profile.WallpapersSet = wallpapers;
                    }

                    // Extract "Caused X orgasms"
                    var causedMatch = Regex.Match(text, @"Caused (\d+) orgasms");
                    if (causedMatch.Success && int.TryParse(causedMatch.Groups[1].Value, out int caused))
                    {
                        profile.OrgasmsCaused = caused;
                    }

                    // Extract "Had X orgasms in the last 7 days"
                    var recentMatch = Regex.Match(text, @"Had (\d+) orgasms in the last 7 days");
                    if (recentMatch.Success && int.TryParse(recentMatch.Groups[1].Value, out int recent))
                    {
                        profile.RecentOrgasms = recent;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"HtmlParser: Error extracting activity stats: {ex.Message}");
        }
    }

    private void ExtractLastOnlineInfo(HtmlDocument doc, UserProfile profile)
    {
        try
        {
            var timeNode = doc.DocumentNode
                .SelectSingleNode("//time[@datetime]");

            if (timeNode != null)
            {
                var datetimeAttr = timeNode.GetAttributeValue("datetime", "");
                var displayText = timeNode.InnerText?.Trim();

                if (!string.IsNullOrEmpty(datetimeAttr) && 
                    DateTime.TryParse(datetimeAttr, null, DateTimeStyles.RoundtripKind, out DateTime lastOnline))
                {
                    profile.LastOnline = lastOnline;
                    profile.LastOnlineText = displayText ?? "";
                    _logger.Add($"HtmlParser: Found last online: {lastOnline} ({displayText})");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"HtmlParser: Error extracting last online info: {ex.Message}");
        }
    }

    private void ExtractRecentWallpapers(HtmlDocument doc, UserProfile profile)
    {
        try
        {
            var wallpaperNodes = doc.DocumentNode
                .SelectNodes("//div[@class='past-link-posts']//a");

            if (wallpaperNodes != null)
            {
                foreach (var node in wallpaperNodes)
                {
                    var href = node.GetAttributeValue("href", "");
                    var imgNode = node.SelectSingleNode(".//img");
                    var src = imgNode?.GetAttributeValue("src", "") ?? "";

                    if (!string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(src))
                    {
                        profile.RecentWallpapers.Add(new RecentWallpaper
                        {
                            ImageUrl = href,
                            ThumbnailUrl = src
                        });
                    }
                }

                _logger.Add($"HtmlParser: Found {profile.RecentWallpapers.Count} recent wallpapers");
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"HtmlParser: Error extracting recent wallpapers: {ex.Message}");
        }
    }

    private void ExtractActivityChart(HtmlDocument doc, UserProfile profile)
    {
        try
        {
            var scriptNodes = doc.DocumentNode.SelectNodes("//script");
            if (scriptNodes != null)
            {
                foreach (var script in scriptNodes)
                {
                    var content = script.InnerText;
                    if (content.Contains("new Chartkick") && content.Contains("AreaChart"))
                    {
                        // Extract chart data using regex
                        var match = Regex.Match(content, @"\[\[""([^""]+)"",(\d+)\](?:,\[""([^""]+)"",(\d+)\])*\]");
                        if (match.Success)
                        {
                            var dataPattern = @"\[""([^""]+)"",(\d+)\]";
                            var dataMatches = Regex.Matches(content, dataPattern);

                            foreach (Match dataMatch in dataMatches)
                            {
                                if (DateTime.TryParse(dataMatch.Groups[1].Value, out DateTime date) &&
                                    int.TryParse(dataMatch.Groups[2].Value, out int value))
                                {
                                    profile.ActivityChart.Add(new ActivityDataPoint
                                    {
                                        Date = date,
                                        Value = value
                                    });
                                }
                            }
                        }

                        _logger.Add($"HtmlParser: Found {profile.ActivityChart.Count} activity data points");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"HtmlParser: Error extracting activity chart: {ex.Message}");
        }
    }

    private void ExtractEnhancedLinkInfo(HtmlDocument doc, UserProfile profile)
    {
        try
        {
            var linkNodes = doc.DocumentNode.SelectNodes("//div[@class='link']");
            if (linkNodes != null)
            {
                var linkIndex = 0;
                foreach (var linkNode in linkNodes)
                {
                    if (linkIndex < profile.Links.Count)
                    {
                        var link = profile.Links[linkIndex];
                        
                        // Extract theme
                        var themeNode = linkNode.SelectSingleNode(".//div[@class='link--theme']//span");
                        if (themeNode != null)
                        {
                            link.Theme = themeNode.InnerText?.Trim();
                        }

                        // Extract abilities
                        var abilityNodes = linkNode.SelectNodes(".//div[@class='link--abilities']//span//ion-icon");
                        if (abilityNodes != null)
                        {
                            foreach (var abilityNode in abilityNodes)
                            {
                                var ability = abilityNode.GetAttributeValue("name", "");
                                if (!string.IsNullOrEmpty(ability))
                                {
                                    link.Abilities.Add(ability);
                                }
                            }
                        }

                        // Extract blacklist tags
                        var blacklistNodes = linkNode.SelectNodes(".//div[@class='link--blacklist']//span");
                        if (blacklistNodes != null)
                        {
                            foreach (var tagNode in blacklistNodes)
                            {
                                var tag = tagNode.InnerText?.Trim();
                                if (!string.IsNullOrEmpty(tag))
                                {
                                    link.BlacklistTags.Add(tag);
                                }
                            }
                        }

                        // Extract device in use
                        var deviceNode = linkNode.SelectSingleNode(".//div[@class='link--device-in-use']//span");
                        if (deviceNode != null)
                        {
                            link.DeviceInUse = deviceNode.InnerText?.Trim();
                        }

                        linkIndex++;
                    }
                }

                // Extract response information
                var responseNodes = doc.DocumentNode.SelectNodes("//div[@class='link--response']");
                if (responseNodes != null)
                {
                    var responseIndex = 0;
                    foreach (var responseNode in responseNodes)
                    {
                        if (responseIndex < profile.Links.Count)
                        {
                            var link = profile.Links[responseIndex];
                            var responseText = responseNode.InnerText?.Trim();
                            
                            if (!string.IsNullOrEmpty(responseText))
                            {
                                link.HasResponse = true;
                                
                                // Extract emoji and text
                                var emojiMatch = Regex.Match(responseText, @"(\p{Cs}|\p{So})+");
                                if (emojiMatch.Success)
                                {
                                    link.ResponseEmoji = emojiMatch.Value;
                                    link.ResponseDisplayText = responseText.Replace(emojiMatch.Value, "").Replace(":", "").Trim();
                                }
                                else
                                {
                                    link.ResponseDisplayText = responseText;
                                }
                            }

                            responseIndex++;
                        }
                    }
                }

                _logger.Add($"HtmlParser: Enhanced {linkIndex} links with HTML data");
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"HtmlParser: Error extracting enhanced link info: {ex.Message}");
        }
    }
}
