using System.Text.Json.Serialization;

namespace AnonWallClient.Models; // Changed namespace

public class LinkData
{
    [JsonPropertyName("post_url")]
    public string? PostUrl { get; set; }

    [JsonPropertyName("uploader_name")]
    public string? UploaderName { get; set; }
}