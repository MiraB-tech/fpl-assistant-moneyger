using System.Text.Json.Serialization;

namespace AssistantMoneyger.Api.Fpl.Dtos;

public class EntryPicksDto
{
    [JsonPropertyName("picks")]
    public List<PickDto> Picks { get; set; } = [];

    [JsonPropertyName("entry_history")]
    public EntryHistoryDto EntryHistory { get; set; } = new();
}

public class PickDto
{
    [JsonPropertyName("element")]
    public int Element { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("is_captain")]
    public bool IsCaptain { get; set; }

    [JsonPropertyName("is_vice_captain")]
    public bool IsViceCaptain { get; set; }

    [JsonPropertyName("multiplier")]
    public int Multiplier { get; set; }
}

public class EntryHistoryDto
{
    // FPL reports these in tenths of a million (e.g. 1003 = £100.3m).
    [JsonPropertyName("bank")]
    public int Bank { get; set; }

    [JsonPropertyName("value")]
    public int Value { get; set; }
}
