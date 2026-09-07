using System.Security.Claims;
using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Fpl;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssistantMoneyger.Api.Features.Squad;

// Matches the frontend's SquadPick type exactly.
public record SquadPickResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("position")] string Position,
    [property: JsonPropertyName("team")] string Team,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("xP")] double Xp,
    [property: JsonPropertyName("squad_position")] int SquadPosition,
    [property: JsonPropertyName("is_captain")] bool IsCaptain,
    [property: JsonPropertyName("is_vice_captain")] bool IsViceCaptain,
    [property: JsonPropertyName("multiplier")] int Multiplier);

public record SquadResponse(
    [property: JsonPropertyName("gameweek")] int Gameweek,
    [property: JsonPropertyName("bank")] decimal Bank,
    [property: JsonPropertyName("team_value")] decimal TeamValue,
    [property: JsonPropertyName("picks")] List<SquadPickResponse> Picks);

// Joins one user's live FPL picks against that gameweek's already-cached
// predictions. No per-user cache — this is cheap enough to compute fresh
// on every request, and the expensive shared work (predictions) is
// already rate-limited by RefreshPredictions.
public static class GetSquad
{
    public static async Task<IResult> Handle(
        int gw,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        FplApiClient fpl)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Json(new { error = "not logged in" }, statusCode: 401);
        }

        if (user.FplTeamId is not int teamId)
        {
            return Results.BadRequest(new { error = "register your FPL team ID first" });
        }

        var picksDto = await fpl.GetEntryPicksAsync(teamId, gw);
        if (picksDto is null)
        {
            return Results.NotFound(new { error = "couldn't load squad picks for that gameweek" });
        }

        var predictionsByPlayerId = await db.Predictions
            .Where(p => p.Gw == gw)
            .ToDictionaryAsync(p => p.PlayerId);

        var picks = new List<SquadPickResponse>();
        foreach (var pick in picksDto.Picks)
        {
            // Shouldn't normally happen — every owned player should be in
            // that gameweek's predictions — but skip rather than crash if
            // the data ever gets out of sync.
            if (!predictionsByPlayerId.TryGetValue(pick.Element, out var prediction))
            {
                continue;
            }

            picks.Add(new SquadPickResponse(
                prediction.PlayerId, prediction.Name, prediction.Position, prediction.Team,
                prediction.Price, prediction.Xp,
                pick.Position, pick.IsCaptain, pick.IsViceCaptain, pick.Multiplier));
        }

        var squad = new SquadResponse(
            gw,
            picksDto.EntryHistory.Bank / 10m,
            picksDto.EntryHistory.Value / 10m,
            picks);

        return Results.Ok(squad);
    }

    public static IEndpointRouteBuilder MapSquadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/squad", Handle).RequireAuthorization();
        return app;
    }
}
