using AssistantMoneyger.Api.Fpl.Dtos;

namespace AssistantMoneyger.Api.Features.Predictions;

// A player's expected points, broken into the pieces that produced it —
// kept transparent/explainable (per the project's own stated goal) rather
// than a single opaque number.
public record PlayerPrediction(
    int Id, string Name, string Position, string Team, double Price,
    double StartProbability, double CleanSheetProbability, double ExpectedGoals, double ExpectedAssists,
    double Xp);

// Estimates expected points using FPL's ACTUAL scoring rules (appearance,
// goals, assists, clean sheets, goals conceded, saves) applied to
// probabilistic estimates of what a player will do this gameweek — not
// an abstract 0-1 ranking score. A genuine expectation blends every
// possible outcome into one number, so e.g. a goalkeeper's clean-sheet
// points show up as something like "1.8" (his clean-sheet chance times
// the 4 points on offer), not a guess at either "4" or "0" alone.
public static class XpEngine
{
    // FPL's real points-per-outcome table, by position.
    private static readonly Dictionary<string, int> GoalPoints = new()
    {
        ["Goalkeeper"] = 6,
        ["Defender"] = 6,
        ["Midfielder"] = 5,
        ["Forward"] = 4,
    };

    private const int AssistPoints = 3;

    private static readonly Dictionary<string, int> CleanSheetPoints = new()
    {
        ["Goalkeeper"] = 4,
        ["Defender"] = 4,
        ["Midfielder"] = 1,
        ["Forward"] = 0,
    };

    // Probability this player plays a meaningful share of the match (the
    // ~60-minute threshold FPL itself uses for appearance/clean-sheet
    // points), combining FPL's own injury/rotation flag with how much of
    // the season-to-date they've actually played.
    public static double StartProbability(ElementDto player, int gamesPlayedSoFar)
    {
        var fitness = (player.ChanceOfPlayingNextRound ?? 100) / 100.0;

        // No games played yet this season for anyone (e.g. GW1) — can't
        // judge from minutes, so don't let a 0-minutes signal zero out
        // every player's prediction; treat it as a genuine unknown.
        if (gamesPlayedSoFar <= 0)
        {
            return fitness;
        }

        var seasonShare = Math.Clamp(player.Minutes / (gamesPlayedSoFar * 90.0), 0, 1);
        // A player who's featured in most of the available minutes is
        // treated as a nailed-on starter (100%) rather than punished for
        // not literally playing every single one.
        var reliability = Math.Min(1.0, seasonShare * 1.3);

        return fitness * reliability;
    }

    // FPL's own fixture difficulty is already a 1 (easiest) to 5
    // (hardest) rating of exactly this — how likely the opponent is to
    // score/be kept out — so it's the natural, already-available input
    // for a clean-sheet estimate rather than inventing a new one from
    // team-strength numbers. Calibrated against typical Premier League
    // clean-sheet rates (top team at home vs. bottom team: ~55%; hard
    // away trip against a strong attack: ~10%) — a transparent,
    // tunable starting point, not a fitted statistical model.
    public static double CleanSheetProbability(int fixtureDifficulty)
    {
        return Math.Clamp(0.55 - (fixtureDifficulty - 1) * 0.11, 0.05, 0.55);
    }

    public static double CalculateXp(
        ElementDto player, string position, double startProbability, double cleanSheetProbability)
    {
        var appearance = startProbability * 2;
        var goals = player.ExpectedGoalsPer90 * startProbability * GoalPoints[position];
        var assists = player.ExpectedAssistsPer90 * startProbability * AssistPoints;
        var cleanSheet = startProbability * cleanSheetProbability * CleanSheetPoints[position];

        var concededPenalty = 0.0;
        var saves = 0.0;
        if (position is "Goalkeeper" or "Defender")
        {
            // Rough Poisson-free approximation: when not kept clean, a
            // team concedes on average ~1.6 goals — that's the -1-per-2
            // rule's expected bite for GK/DEF.
            var expectedGoalsConceded = (1 - cleanSheetProbability) * 1.6;
            concededPenalty = -startProbability * (expectedGoalsConceded / 2);
        }
        if (position == "Goalkeeper")
        {
            // Facing more shots (i.e. a lower clean-sheet chance) means
            // more save-point opportunities — a coarse stand-in for a
            // real shots-faced model.
            saves = startProbability * (1 - cleanSheetProbability) * 1.2;
        }

        return appearance + goals + assists + cleanSheet + concededPenalty + saves;
    }

    public static int? GetFixtureDifficulty(ElementDto player, IReadOnlyList<FixtureDto> fixtures, int targetGw)
    {
        foreach (var fixture in fixtures)
        {
            if (fixture.Event != targetGw) continue;
            if (fixture.TeamH == player.Team) return fixture.TeamHDifficulty;
            if (fixture.TeamA == player.Team) return fixture.TeamADifficulty;
        }
        return null;
    }

    public static List<PlayerPrediction> BuildPredictions(
        BootstrapStaticDto bootstrap, IReadOnlyList<FixtureDto> fixtures, int targetGw)
    {
        var positionNames = bootstrap.ElementTypes.ToDictionary(et => et.Id, et => et.SingularName);
        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id, t => t);
        var gamesPlayedSoFar = bootstrap.Events.Count(e => e.Finished);

        var predictions = new List<PlayerPrediction>();
        foreach (var player in bootstrap.Elements)
        {
            var difficulty = GetFixtureDifficulty(player, fixtures, targetGw);
            if (difficulty is null)
            {
                // No fixture this gameweek (e.g. a blank gameweek) — skip
                // rather than guess at a made-up difficulty.
                continue;
            }

            var position = positionNames[player.ElementType];
            var startProbability = StartProbability(player, gamesPlayedSoFar);
            var cleanSheetProbability = CleanSheetProbability(difficulty.Value);
            var xp = CalculateXp(player, position, startProbability, cleanSheetProbability);

            predictions.Add(new PlayerPrediction(
                player.Id,
                player.WebName,
                position,
                teamsById[player.Team].ShortName,
                player.NowCost / 10.0,
                Math.Round(startProbability, 3),
                Math.Round(cleanSheetProbability, 3),
                Math.Round(player.ExpectedGoalsPer90 * startProbability, 3),
                Math.Round(player.ExpectedAssistsPer90 * startProbability, 3),
                Math.Round(xp, 2)));
        }

        return predictions;
    }
}
