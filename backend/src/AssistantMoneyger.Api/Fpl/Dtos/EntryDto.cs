using System.Text.Json.Serialization;

namespace AssistantMoneyger.Api.Fpl.Dtos;

// Only used as an existence check (does this team ID exist?) — we don't
// need most of what FPL returns here, just enough to deserialize cleanly.
public class EntryDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
