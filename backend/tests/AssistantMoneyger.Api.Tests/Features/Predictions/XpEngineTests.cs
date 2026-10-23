using AssistantMoneyger.Api.Features.Predictions;
using AssistantMoneyger.Api.Fpl.Dtos;
using Xunit;

namespace AssistantMoneyger.Api.Tests.Features.Predictions;

public class XpEngineTests
{
    private static ElementDto Player(int id, int minutes = 900, int? chanceOfPlaying = null,
        double xg90 = 0, double xa90 = 0, int team = 1, int elementType = 1) => new()
    {
        Id = id,
        WebName = $"Player{id}",
        ElementType = elementType,
        Team = team,
        NowCost = 100,
        PointsPerGame = "2.0",
        Minutes = minutes,
        ChanceOfPlayingNextRound = chanceOfPlaying,
        ExpectedGoalsPer90 = xg90,
        ExpectedAssistsPer90 = xa90,
    };

    [Fact]
    public void StartProbability_FitAndPlayedEveryMinute_IsOne()
    {
        var player = Player(1, minutes: 900); // 10 games worth, all 90 minutes
        var result = XpEngine.StartProbability(player, gamesPlayedSoFar: 10);
        Assert.Equal(1.0, result, precision: 3);
    }

    [Fact]
    public void StartProbability_InjuryDoubtHalvesProbability()
    {
        var player = Player(1, minutes: 900, chanceOfPlaying: 50);
        var result = XpEngine.StartProbability(player, gamesPlayedSoFar: 10);
        Assert.Equal(0.5, result, precision: 3);
    }

    [Fact]
    public void StartProbability_NeverPlayedThisSeason_IsZero()
    {
        var player = Player(1, minutes: 0);
        var result = XpEngine.StartProbability(player, gamesPlayedSoFar: 10);
        Assert.Equal(0.0, result, precision: 3);
    }

    [Fact]
    public void StartProbability_NoGamesPlayedYetBySeason_FallsBackToFitnessOnly()
    {
        // GW1: nobody has any minutes yet, so judging by minutes would
        // wrongly zero out every player. Fitness alone should carry it.
        var player = Player(1, minutes: 0, chanceOfPlaying: 100);
        var result = XpEngine.StartProbability(player, gamesPlayedSoFar: 0);
        Assert.Equal(1.0, result, precision: 3);
    }

    [Fact]
    public void StartProbability_PartialSeasonMinutes_ScalesProportionally()
    {
        // Played half the available minutes -> reliability factor kicks
        // in below the "treat as nailed-on" ceiling.
        var player = Player(1, minutes: 450); // half of 10 games * 90
        var result = XpEngine.StartProbability(player, gamesPlayedSoFar: 10);
        Assert.Equal(0.65, result, precision: 3); // 0.5 * 1.3
    }

    [Fact]
    public void CleanSheetProbability_EasiestFixture_ReturnsMaximum()
    {
        Assert.Equal(0.55, XpEngine.CleanSheetProbability(1), precision: 3);
    }

    [Fact]
    public void CleanSheetProbability_HardestFixture_ReturnsMinimum()
    {
        Assert.Equal(0.11, XpEngine.CleanSheetProbability(5), precision: 3);
    }

    [Fact]
    public void CalculateXp_Goalkeeper_MatchesHandComputedExpectedPoints()
    {
        var player = Player(1, minutes: 900, elementType: 1); // GK, no xG/xA
        var startProbability = 1.0;
        var cleanSheetProbability = 0.55; // easiest fixture

        var xp = XpEngine.CalculateXp(player, "Goalkeeper", startProbability, cleanSheetProbability);

        // appearance 2.0 + clean sheet (1*0.55*4=2.2) + conceded penalty
        // (-(1-0.55)*1.6/2 = -0.36) + saves ((1-0.55)*1.2 = 0.54) = 4.38
        Assert.Equal(4.38, xp, precision: 2);
    }

    [Fact]
    public void CalculateXp_Forward_HasNoCleanSheetOrSavesContribution()
    {
        var player = Player(1, xg90: 0.5, xa90: 0.1, elementType: 4); // Forward
        var startProbability = 1.0;
        var cleanSheetProbability = 0.33;

        var xp = XpEngine.CalculateXp(player, "Forward", startProbability, cleanSheetProbability);

        // appearance 2.0 + goals (0.5*1*4=2.0) + assists (0.1*1*3=0.3) = 4.3
        Assert.Equal(4.3, xp, precision: 2);
    }

    [Fact]
    public void GetFixtureDifficulty_HomeMatch_ReturnsHomeDifficulty()
    {
        var player = Player(1, team: 5);
        var fixtures = new List<FixtureDto>
        {
            new() { Event = 2, TeamH = 5, TeamA = 9, TeamHDifficulty = 4, TeamADifficulty = 2 },
        };

        Assert.Equal(4, XpEngine.GetFixtureDifficulty(player, fixtures, targetGw: 2));
    }

    [Fact]
    public void GetFixtureDifficulty_NoFixtureThatGameweek_ReturnsNull()
    {
        var player = Player(1, team: 5);
        var fixtures = new List<FixtureDto>
        {
            new() { Event = 3, TeamH = 5, TeamA = 9, TeamHDifficulty = 4, TeamADifficulty = 2 },
        };

        Assert.Null(XpEngine.GetFixtureDifficulty(player, fixtures, targetGw: 2));
    }

    [Fact]
    public void BuildPredictions_SkipsPlayersWithNoFixtureThatGameweek()
    {
        var bootstrap = new BootstrapStaticDto
        {
            Elements = [Player(1, team: 5), Player(2, team: 99)],
            Teams = [new TeamDto { Id = 5, ShortName = "ABC" }, new TeamDto { Id = 99, ShortName = "XYZ" }],
            ElementTypes = [new ElementTypeDto { Id = 1, SingularName = "Goalkeeper" }],
        };
        var fixtures = new List<FixtureDto>
        {
            new() { Event = 2, TeamH = 5, TeamA = 1, TeamHDifficulty = 3, TeamADifficulty = 3 },
        };

        var predictions = XpEngine.BuildPredictions(bootstrap, fixtures, targetGw: 2);

        Assert.Single(predictions);
        Assert.Equal(1, predictions[0].Id);
    }
}
