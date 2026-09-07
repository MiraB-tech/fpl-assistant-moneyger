using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Data.Entities;
using AssistantMoneyger.Api.Fpl;
using Microsoft.EntityFrameworkCore;

namespace AssistantMoneyger.Api.Features.Evaluation;

public record EvaluateRequest(int Gw);

public record EvaluateResult(
    [property: JsonPropertyName("evaluated")] bool Evaluated,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("num_players")] int? NumPlayers);

// Compares a gameweek's saved predictions against what actually happened,
// once. Guards against re-evaluating the same gameweek twice (so
// re-running AdvanceGameweek for the same target doesn't duplicate rows).
public static class EvaluateGameweek
{
    public static async Task<EvaluateResult> RunAsync(AppDbContext db, FplApiClient fpl, int gw, CancellationToken ct = default)
    {
        var alreadyEvaluated = await db.ModelPerformanceLog.AnyAsync(m => m.Gw == gw, ct);
        if (alreadyEvaluated)
        {
            return new EvaluateResult(false, "already evaluated", null);
        }

        var predictions = await db.Predictions.Where(p => p.Gw == gw).ToListAsync(ct);
        if (predictions.Count == 0)
        {
            return new EvaluateResult(false, "no predictions saved for this gameweek yet", null);
        }

        var actualPoints = await fpl.GetLiveEventAsync(gw, ct);
        var now = DateTimeOffset.UtcNow;

        var results = predictions.Select(p =>
        {
            var actual = actualPoints.GetValueOrDefault(p.PlayerId, 0);
            return new Result
            {
                Gw = gw,
                PlayerId = p.PlayerId,
                Name = p.Name,
                Position = p.Position,
                Team = p.Team,
                PredictedPoints = p.Xp,
                ActualPoints = actual,
                Difference = p.Xp - actual,
                EvaluatedAt = now,
            };
        }).ToList();

        db.Results.AddRange(results);

        var meanAbsoluteError = results.Average(r => Math.Abs(r.Difference));
        db.ModelPerformanceLog.Add(new ModelPerformanceLogEntry
        {
            Gw = gw,
            EvaluatedAt = now,
            NumPlayers = results.Count,
            MeanAbsoluteError = Math.Round(meanAbsoluteError, 2),
        });

        await db.SaveChangesAsync(ct);

        return new EvaluateResult(true, null, results.Count);
    }

    public static IEndpointRouteBuilder MapEvaluationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/evaluate", async (EvaluateRequest request, AppDbContext db, FplApiClient fpl) =>
        {
            var result = await RunAsync(db, fpl, request.Gw);
            return Results.Ok(result);
        }).RequireAuthorization();

        return app;
    }
}
