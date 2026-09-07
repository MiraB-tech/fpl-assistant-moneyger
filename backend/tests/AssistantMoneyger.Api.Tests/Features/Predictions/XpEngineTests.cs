using AssistantMoneyger.Api.Features.Predictions;
using AssistantMoneyger.Api.Fpl.Dtos;
using Xunit;

namespace AssistantMoneyger.Api.Tests.Features.Predictions;

public class XpEngineTests
{
    private static ElementDto Player(int id, string pointsPerGame = "2.0", int minutes = 900,
        double xg90 = 0.3, double xa90 = 0.1, int team = 1, int elementType = 4) => new()
    {
        Id = id,
        WebName = $"Player{id}",
        ElementType = elementType,
        Team = team,
        NowCost = 100,
        PointsPerGame = pointsPerGame,
        ExpectedGoalsPer90 = xg90,
        ExpectedAssistsPer90 = xa90,
        Minutes = minutes,
    };

    [Fact]
    public void RecentForm_WithNoPreviousGameweek_FallsBackToPointsPerGame()
    {
        var player = Player(1, pointsPerGame: "4.5");

        var result = XpEngine.RecentForm(player, previousGwPoints: null);

        Assert.Equal(4.5, result);
    }

    [Fact]
    public void RecentForm_WithPreviousGameweek_UsesRealPointsNotPointsPerGame()
    {
        // This is the exact bug that was fixed in the Python version:
        // once a previous gameweek exists, its real points must be used
        // instead of any "current form"-style rolling average.
        var player = Player(1, pointsPerGame: "4.5");
        var previousGwPoints = new Dictionary<int, int> { [1] = 9 };

        var result = XpEngine.RecentForm(player, previousGwPoints);

        Assert.Equal(9, result);
    }

    [Fact]
    public void RecentForm_PlayerNotInPreviousGameweek_DefaultsToZero()
    {
        var player = Player(1);
        var previousGwPoints = new Dictionary<int, int> { [999] = 9 };

        var result = XpEngine.RecentForm(player, previousGwPoints);

        Assert.Equal(0, result);
    }

    [Fact]
    public void FixtureDifficulty_HomeMatch_Returns6MinusHomeDifficulty()
    {
        var player = Player(1, team: 5);
        var fixtures = new List<FixtureDto>
        {
            new() { Event = 2, TeamH = 5, TeamA = 9, TeamHDifficulty = 4, TeamADifficulty = 2 },
        };

        var result = XpEngine.FixtureDifficulty(player, fixtures, targetGw: 2);

        Assert.Equal(2, result); // 6 - 4
    }

    [Fact]
    public void FixtureDifficulty_AwayMatch_Returns6MinusAwayDifficulty()
    {
        var player = Player(1, team: 9);
        var fixtures = new List<FixtureDto>
        {
            new() { Event = 2, TeamH = 5, TeamA = 9, TeamHDifficulty = 4, TeamADifficulty = 2 },
        };

        var result = XpEngine.FixtureDifficulty(player, fixtures, targetGw: 2);

        Assert.Equal(4, result); // 6 - 2
    }

    [Fact]
    public void FixtureDifficulty_NoFixtureThatGameweek_ReturnsNull()
    {
        var player = Player(1, team: 5);
        var fixtures = new List<FixtureDto>
        {
            new() { Event = 3, TeamH = 5, TeamA = 9, TeamHDifficulty = 4, TeamADifficulty = 2 },
        };

        var result = XpEngine.FixtureDifficulty(player, fixtures, targetGw: 2);

        Assert.Null(result);
    }

    [Fact]
    public void BuildFeatures_SkipsPlayersWithNoFixtureThatGameweek()
    {
        var bootstrap = new BootstrapStaticDto
        {
            Elements = [Player(1, team: 5), Player(2, team: 99)],
            Teams = [new TeamDto { Id = 5, ShortName = "ABC" }, new TeamDto { Id = 99, ShortName = "XYZ" }],
            ElementTypes = [new ElementTypeDto { Id = 4, SingularName = "Forward" }],
        };
        var fixtures = new List<FixtureDto>
        {
            new() { Event = 2, TeamH = 5, TeamA = 1, TeamHDifficulty = 3, TeamADifficulty = 3 },
        };

        var features = XpEngine.BuildFeatures(bootstrap, fixtures, targetGw: 2, previousGwPoints: null);

        Assert.Single(features);
        Assert.Equal(1, features[0].Id);
    }

    [Fact]
    public void CalculateXp_TwoPlayers_MinMaxNormalizesToZeroAndOne()
    {
        // With exactly two players, whichever one is smaller on every
        // feature should normalize to 0 and score xP = 0; the other
        // should normalize to 1 on every feature and score xP = 1.0
        // (the four weights sum to exactly 1.0).
        var features = new List<PlayerFeatures>
        {
            new(1, "Low", "Forward", "ABC", 5.0, RecentForm: 1, XaXgPer90: 0.1, FixtureDifficulty: 1, MinutesReliability: 0.1),
            new(2, "High", "Forward", "ABC", 5.0, RecentForm: 5, XaXgPer90: 0.5, FixtureDifficulty: 5, MinutesReliability: 0.9),
        };

        var predictions = XpEngine.CalculateXp(features);

        var low = predictions.Single(p => p.Id == 1);
        var high = predictions.Single(p => p.Id == 2);

        Assert.Equal(0.0, low.Xp);
        Assert.Equal(1.0, high.Xp);
    }
}
