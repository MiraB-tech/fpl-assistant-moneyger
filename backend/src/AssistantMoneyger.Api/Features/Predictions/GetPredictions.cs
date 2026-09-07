using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Data.Entities;
using AssistantMoneyger.Api.Fpl;
using Microsoft.EntityFrameworkCore;

namespace AssistantMoneyger.Api.Features.Predictions;

// Matches the frontend's Player type in frontend/src/types.ts exactly.
public record PlayerResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("position")] string Position,
    [property: JsonPropertyName("team")] string Team,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("xP")] double Xp)
{
    public static PlayerResponse From(Prediction p) =>
        new(p.PlayerId, p.Name, p.Position, p.Team, p.Price, p.Xp);
}

public record RefreshPredictionsRequest(int Gw);

public static class PredictionsEndpoints
{
    public static IEndpointRouteBuilder MapPredictionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/predictions", async (int gw, AppDbContext db) =>
        {
            var predictions = await db.Predictions
                .Where(p => p.Gw == gw)
                .OrderByDescending(p => p.Xp)
                .Select(p => PlayerResponse.From(p))
                .ToListAsync();

            return Results.Ok(predictions);
        }).RequireAuthorization();

        app.MapPost("/api/predictions/refresh", async (RefreshPredictionsRequest request, AppDbContext db, FplApiClient fpl) =>
        {
            var result = await RefreshPredictions.RunAsync(db, fpl, request.Gw);
            return Results.Ok(result);
        }).RequireAuthorization();

        return app;
    }
}
