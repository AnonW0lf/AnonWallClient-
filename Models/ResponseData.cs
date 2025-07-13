using System.Text.Json.Serialization;

namespace AnonWallClient.Models;

public class ResponseData
{
    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; }

    [JsonPropertyName("type")]
    public string ResponseType { get; set; }
}