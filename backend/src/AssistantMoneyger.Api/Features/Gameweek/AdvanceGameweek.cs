using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Features.Evaluation;
using AssistantMoneyger.Api.Features.Predictions;
using AssistantMoneyger.Api.Fpl;

namespace AssistantMoneyger.Api.Features.Gameweek;

public record AdvanceRequest([property: JsonPropertyName("next_gw")] int NextGw);

public record AdvanceResponse(
    [property: JsonPropertyName("gw")] int Gw,
    [property: JsonPropertyName("evaluation")] EvaluateResult Evaluation,
    [property: JsonPropertyName("refresh")] RefreshResult Refresh);

// The one button the frontend actually calls: grade the gameweek that
// just finished (if there is one, and it's not already graded), then
// make sure the next gameweek's predictions are fresh.
public static class AdvanceGameweek
{
    public static async Task<IResult> Handle(AdvanceRequest request, AppDbContext db, FplApiClient fpl)
    {
        var evaluation = request.NextGw > 1
            ? await EvaluateGameweek.RunAsync(db, fpl, request.NextGw - 1)
            : new EvaluateResult(false, "no earlier gameweek", null);

        var refresh = await RefreshPredictions.RunAsync(db, fpl, request.NextGw);

        return Results.Ok(new AdvanceResponse(request.NextGw, evaluation, refresh));
    }

    public static IEndpointRouteBuilder MapGameweekEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gameweek/current", GetCurrentGameweek.Handle).RequireAuthorization();
        app.MapPost("/api/gameweek/advance", Handle).RequireAuthorization();
        return app;
    }
}
