using System.Text.Json.Serialization;

namespace AssistantMoneyger.Api.Fpl.Dtos;

public class FixtureDto
{
    [JsonPropertyName("event")]
    public int? Event { get; set; }

    [JsonPropertyName("team_h")]
    public int TeamH { get; set; }

    [JsonPropertyName("team_a")]
    public int TeamA { get; set; }

    [JsonPropertyName("team_h_difficulty")]
    public int TeamHDifficulty { get; set; }

    [JsonPropertyName("team_a_difficulty")]
    public int TeamADifficulty { get; set; }
}
