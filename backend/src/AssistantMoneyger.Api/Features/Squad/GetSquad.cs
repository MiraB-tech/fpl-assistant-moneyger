using System.Security.Claims;
using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Fpl;
using AssistantMoneyger.Api.Fpl.Dtos;
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
    [property: JsonPropertyName("current_points")] int CurrentPoints,
    [property: JsonPropertyName("squad_position")] int SquadPosition,
    [property: JsonPropertyName("is_captain")] bool IsCaptain,
    [property: JsonPropertyName("is_vice_captain")] bool IsViceCaptain,
    [property: JsonPropertyName("multiplier")] int Multiplier);

public record SquadResponse(
    [property: JsonPropertyName("gameweek")] int Gameweek,
    [property: JsonPropertyName("bank")] decimal Bank,
    [property: JsonPropertyName("team_value")] decimal TeamValue,
    [property: JsonPropertyName("picks")] List<SquadPickResponse> Picks);

// Joins one user's live FPL picks against a gameweek's already-cached
// predictions. No per-user cache — this is cheap enough to compute fresh
// on every request, and the expensive shared work (predictions) is
// already rate-limited by RefreshPredictions.
public static class GetSquad
{
    public static async Task<IResult> Handle(
        int gw,
        int? picksGw,
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

        // FPL only has a "picks" record for a gameweek once its deadline
        // has actually passed — a future gameweek 404s. To plan ahead
        // (e.g. Best XI for next week), we still need your REAL current
        // squad, so picksGw (defaults to gw) lets callers ask for e.g.
        // "picks from gw3, predictions from gw4" instead of picks that
        // don't exist yet.
        var effectivePicksGw = picksGw ?? gw;

        EntryPicksDto picksDto;
        try
        {
            picksDto = await fpl.GetEntryPicksAsync(teamId, effectivePicksGw)
                ?? throw new HttpRequestException("empty response");
        }
        catch (HttpRequestException)
        {
            return Results.NotFound(new
            {
                error = $"couldn't load squad picks for GW{effectivePicksGw} — its deadline may not have passed yet",
            });
        }

        var predictionsByPlayerId = await db.Predictions
            .Where(p => p.Gw == gw)
            .ToDictionaryAsync(p => p.PlayerId);

        // Real points scored so far in the gameweek the picks belong to
        // (0 for every player if it hasn't kicked off yet). A live-data
        // hiccup here shouldn't break the whole squad view, so fall back
        // to "nobody's scored yet" rather than fail.
        Dictionary<int, int> currentPoints;
        try
        {
            currentPoints = await fpl.GetLiveEventAsync(effectivePicksGw);
        }
        catch (HttpRequestException)
        {
            currentPoints = [];
        }

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
                prediction.Price, prediction.Xp, currentPoints.GetValueOrDefault(pick.Element, 0),
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
