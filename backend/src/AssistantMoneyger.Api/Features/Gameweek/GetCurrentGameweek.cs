using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Fpl;

namespace AssistantMoneyger.Api.Features.Gameweek;

public record GameweekResponse(
    [property: JsonPropertyName("current_gw")] int? CurrentGw,
    [property: JsonPropertyName("next_gw")] int? NextGw);

public static class GetCurrentGameweek
{
    public static async Task<IResult> Handle(FplApiClient fpl)
    {
        var bootstrap = await fpl.GetBootstrapStaticAsync();
        var events = bootstrap?.Events ?? [];

        var currentGw = events.FirstOrDefault(e => e.IsCurrent)?.Id;
        var nextGw = events.FirstOrDefault(e => e.IsNext)?.Id;

        return Results.Ok(new GameweekResponse(currentGw, nextGw));
    }
}
