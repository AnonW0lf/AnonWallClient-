using System.Text.Json.Serialization;

namespace AnonWallClient.Models;

public class UserProfileLink
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("expires")]
    public DateTime? Expires { get; set; }

    [JsonPropertyName("terms")]
    public string Terms { get; set; } = string.Empty;

    [JsonPropertyName("blacklist")]
    public string Blacklist { get; set; } = string.Empty;

    [JsonPropertyName("post_url")]
    public string PostUrl { get; set; } = string.Empty;

    [JsonPropertyName("post_thumbnail_url")]
    public string PostThumbnailUrl { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("response_type")]
    public string? ResponseType { get; set; }

    [JsonPropertyName("response_text")]
    public string? ResponseText { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("online")]
    public bool Online { get; set; }

    [JsonPropertyName("post_description")]
    public string? PostDescription { get; set; }

    // HTML-extracted properties
    public string? Theme { get; set; }
    public List<string> Abilities { get; set; } = new();
    public List<string> BlacklistTags { get; set; } = new();
    public string? DeviceInUse { get; set; }
    public bool HasResponse { get; set; }
    public string? ResponseEmoji { get; set; }
    public string? ResponseDisplayText { get; set; }
}

public class ActivityDataPoint
{
    public DateTime Date { get; set; }
    public int Value { get; set; }
}

public class RecentWallpaper
{
    public string ImageUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
}

public class UserProfile
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("set_count")]
    public int SetCount { get; set; }

    [JsonPropertyName("is_reporter")]
    public bool IsReporter { get; set; }

    [JsonPropertyName("is_cutie")]
    public bool IsCutie { get; set; }

    [JsonPropertyName("is_supporter")]
    public bool IsSupporter { get; set; }

    [JsonPropertyName("online")]
    public bool Online { get; set; }

    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; set; }

    [JsonPropertyName("friend")]
    public bool Friend { get; set; }

    [JsonPropertyName("self")]
    public bool Self { get; set; }

    [JsonPropertyName("links")]
    public List<UserProfileLink> Links { get; set; } = new();

    [JsonPropertyName("flair")]
    public string Flair { get; set; } = string.Empty;

    [JsonPropertyName("master")]
    public bool Master { get; set; }

    [JsonPropertyName("pets")]
    public List<object> Pets { get; set; } = new();

    // HTML-extracted properties
    public string ProfileDescription { get; set; } = string.Empty;
    public int WallpapersSet { get; set; }
    public int OrgasmsCaused { get; set; }
    public int RecentOrgasms { get; set; }
    public DateTime LastOnline { get; set; }
    public string LastOnlineText { get; set; } = string.Empty;
    public List<RecentWallpaper> RecentWallpapers { get; set; } = new();
    public List<ActivityDataPoint> ActivityChart { get; set; } = new();
    public bool HasHtmlData { get; set; } = false;
}
