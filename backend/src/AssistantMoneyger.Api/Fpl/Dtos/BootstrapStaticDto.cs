using System.Text.Json.Serialization;

namespace AssistantMoneyger.Api.Fpl.Dtos;

public class BootstrapStaticDto
{
    [JsonPropertyName("elements")]
    public List<ElementDto> Elements { get; set; } = [];

    [JsonPropertyName("teams")]
    public List<TeamDto> Teams { get; set; } = [];

    [JsonPropertyName("element_types")]
    public List<ElementTypeDto> ElementTypes { get; set; } = [];

    [JsonPropertyName("events")]
    public List<EventDto> Events { get; set; } = [];
}

public class ElementDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("web_name")]
    public string WebName { get; set; } = "";

    [JsonPropertyName("element_type")]
    public int ElementType { get; set; }

    [JsonPropertyName("team")]
    public int Team { get; set; }

    [JsonPropertyName("now_cost")]
    public int NowCost { get; set; }

    [JsonPropertyName("points_per_game")]
    public string PointsPerGame { get; set; } = "0";

    [JsonPropertyName("expected_goals_per_90")]
    public double ExpectedGoalsPer90 { get; set; }

    [JsonPropertyName("expected_assists_per_90")]
    public double ExpectedAssistsPer90 { get; set; }

    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }
}

public class TeamDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("short_name")]
    public string ShortName { get; set; } = "";
}

public class ElementTypeDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("singular_name")]
    public string SingularName { get; set; } = "";
}

public class EventDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("is_current")]
    public bool IsCurrent { get; set; }

    [JsonPropertyName("is_next")]
    public bool IsNext { get; set; }
}
