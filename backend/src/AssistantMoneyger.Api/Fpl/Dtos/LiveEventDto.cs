using System.Text.Json.Serialization;

namespace AssistantMoneyger.Api.Fpl.Dtos;

public class LiveEventDto
{
    [JsonPropertyName("elements")]
    public List<LiveElementDto> Elements { get; set; } = [];
}

public class LiveElementDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("stats")]
    public LiveStatsDto Stats { get; set; } = new();
}

public class LiveStatsDto
{
    [JsonPropertyName("total_points")]
    public int TotalPoints { get; set; }
}
