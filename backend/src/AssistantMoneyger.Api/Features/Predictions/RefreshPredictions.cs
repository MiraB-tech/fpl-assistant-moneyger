using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Data.Entities;
using AssistantMoneyger.Api.Fpl;

namespace AssistantMoneyger.Api.Features.Predictions;

public record RefreshResult(
    [property: JsonPropertyName("refreshed")] bool Refreshed,
    [property: JsonPropertyName("player_count")] int PlayerCount,
    [property: JsonPropertyName("last_refreshed_at")] DateTimeOffset LastRefreshedAt);

// Shared by the standalone POST /api/predictions/refresh endpoint and by
// AdvanceGameweek. The staleness check is what makes this safe for many
// users to trigger at once: only the first caller within a window does
// the real FPL pull + recompute, everyone else gets the cached result.
public static class RefreshPredictions
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(6);

    public static async Task<RefreshResult> RunAsync(AppDbContext db, FplApiClient fpl, int gw, CancellationToken ct = default)
    {
        var run = await db.PredictionRuns.FindAsync([gw], ct);
        if (run is not null && DateTimeOffset.UtcNow - run.LastRefreshedAt < StaleAfter)
        {
            return new RefreshResult(false, run.PlayerCount, run.LastRefreshedAt);
        }

        var bootstrap = await fpl.GetBootstrapStaticAsync(ct)
            ?? throw new InvalidOperationException("FPL bootstrap-static returned nothing");
        var fixtures = await fpl.GetFixturesAsync(ct) ?? [];

        var previousGw = gw - 1;
        var previousGwPoints = previousGw >= 1 ? await fpl.GetLiveEventAsync(previousGw, ct) : null;

        var features = XpEngine.BuildFeatures(bootstrap, fixtures, gw, previousGwPoints);
        var predictions = XpEngine.CalculateXp(features);

        var existing = db.Predictions.Where(p => p.Gw == gw);
        db.Predictions.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        foreach (var prediction in predictions)
        {
            db.Predictions.Add(new Prediction
            {
                Gw = gw,
                PlayerId = prediction.Id,
                Name = prediction.Name,
                Position = prediction.Position,
                Team = prediction.Team,
                Price = (decimal)prediction.Price,
                RecentForm = prediction.RecentForm,
                XaXgPer90 = prediction.XaXgPer90,
                FixtureDifficulty = prediction.FixtureDifficulty,
                MinutesReliability = prediction.MinutesReliability,
                Xp = prediction.Xp,
                ComputedAt = now,
            });
        }

        if (run is null)
        {
            run = new PredictionRun { Gw = gw };
            db.PredictionRuns.Add(run);
        }
        run.LastRefreshedAt = now;
        run.PlayerCount = predictions.Count;

        await db.SaveChangesAsync(ct);

        return new RefreshResult(true, predictions.Count, now);
    }
}
