using System.Globalization;
using AssistantMoneyger.Api.Fpl.Dtos;

namespace AssistantMoneyger.Api.Features.Predictions;

public record PlayerFeatures(
    int Id, string Name, string Position, string Team, double Price,
    double RecentForm, double XaXgPer90, double FixtureDifficulty, double MinutesReliability);

public record PlayerPrediction(
    int Id, string Name, string Position, string Team, double Price,
    double RecentForm, double XaXgPer90, double FixtureDifficulty, double MinutesReliability, double Xp);

// Pure, static, no HttpClient/DbContext — mirrors the Python pipeline's
// build_features.py + predict.py exactly, including the fix for a real
// bug found there: recent form must come from the previous FINISHED
// gameweek's actual points (previousGwPoints), never a live "current
// form" number, or it leaks in-progress results into the prediction.
// previousGwPoints is null only for gameweek 1 (nothing earlier exists
// yet) — that decision is made by the caller, not by this class, so this
// class has no way to accidentally reach for a live field instead.
public static class XpEngine
{
    public const double FormWeight = 0.40;
    public const double XgXaWeight = 0.25;
    public const double FixtureWeight = 0.20;
    public const double MinutesWeight = 0.15;

    public static double MinutesReliability(ElementDto player) => player.Minutes / 3420.0;

    public static double XaXgPer90(ElementDto player) =>
        player.ExpectedGoalsPer90 + player.ExpectedAssistsPer90;

    public static double RecentForm(ElementDto player, IReadOnlyDictionary<int, int>? previousGwPoints)
    {
        if (previousGwPoints is null)
        {
            return double.Parse(player.PointsPerGame, CultureInfo.InvariantCulture);
        }
        return previousGwPoints.GetValueOrDefault(player.Id, 0);
    }

    public static double? FixtureDifficulty(ElementDto player, IReadOnlyList<FixtureDto> fixtures, int targetGw)
    {
        foreach (var fixture in fixtures)
        {
            if (fixture.Event != targetGw) continue;
            if (fixture.TeamH == player.Team) return 6 - fixture.TeamHDifficulty;
            if (fixture.TeamA == player.Team) return 6 - fixture.TeamADifficulty;
        }
        return null;
    }

    public static List<PlayerFeatures> BuildFeatures(
        BootstrapStaticDto bootstrap,
        IReadOnlyList<FixtureDto> fixtures,
        int targetGw,
        IReadOnlyDictionary<int, int>? previousGwPoints)
    {
        var positionNames = bootstrap.ElementTypes.ToDictionary(et => et.Id, et => et.SingularName);
        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id, t => t);

        var features = new List<PlayerFeatures>();
        foreach (var player in bootstrap.Elements)
        {
            var fixtureDifficulty = FixtureDifficulty(player, fixtures, targetGw);
            if (fixtureDifficulty is null)
            {
                // No fixture this gameweek (e.g. a blank gameweek) — skip
                // this player rather than let a missing feature break the
                // min-max normalization for everyone else.
                continue;
            }

            features.Add(new PlayerFeatures(
                Id: player.Id,
                Name: player.WebName,
                Position: positionNames[player.ElementType],
                Team: teamsById[player.Team].ShortName,
                Price: player.NowCost / 10.0,
                RecentForm: RecentForm(player, previousGwPoints),
                XaXgPer90: XaXgPer90(player),
                FixtureDifficulty: fixtureDifficulty.Value,
                MinutesReliability: MinutesReliability(player)));
        }

        return features;
    }

    public static List<PlayerPrediction> CalculateXp(IReadOnlyList<PlayerFeatures> features)
    {
        var (formMin, formMax) = MinMax(features, f => f.RecentForm);
        var (xgaMin, xgaMax) = MinMax(features, f => f.XaXgPer90);
        var (fixMin, fixMax) = MinMax(features, f => f.FixtureDifficulty);
        var (minsMin, minsMax) = MinMax(features, f => f.MinutesReliability);

        var results = new List<PlayerPrediction>();
        foreach (var player in features)
        {
            var formScaled = Rescale(player.RecentForm, formMin, formMax);
            var xgaScaled = Rescale(player.XaXgPer90, xgaMin, xgaMax);
            var fixScaled = Rescale(player.FixtureDifficulty, fixMin, fixMax);
            var minsScaled = Rescale(player.MinutesReliability, minsMin, minsMax);

            var xp = formScaled * FormWeight
                + xgaScaled * XgXaWeight
                + fixScaled * FixtureWeight
                + minsScaled * MinutesWeight;

            results.Add(new PlayerPrediction(
                player.Id, player.Name, player.Position, player.Team, player.Price,
                player.RecentForm, player.XaXgPer90, player.FixtureDifficulty, player.MinutesReliability,
                Math.Round(xp, 2)));
        }

        return results;
    }

    private static (double Min, double Max) MinMax(IReadOnlyList<PlayerFeatures> features, Func<PlayerFeatures, double> selector)
    {
        var values = features.Select(selector).ToList();
        return (values.Min(), values.Max());
    }

    private static double Rescale(double value, double min, double max) => (value - min) / (max - min);
}
