using System.Security.Claims;
using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Features.Auth;
using AssistantMoneyger.Api.Fpl;
using Microsoft.AspNetCore.Identity;

namespace AssistantMoneyger.Api.Features.Team;

public record UpdateTeamRequest([property: JsonPropertyName("fpl_team_id")] int FplTeamId);

public static class UpdateTeam
{
    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/team", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        UpdateTeamRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        FplApiClient fpl)
    {
        try
        {
            await fpl.GetEntryAsync(request.FplTeamId);
        }
        catch (HttpRequestException)
        {
            return Results.BadRequest(new { error = "that FPL team ID doesn't seem to exist" });
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Json(new { error = "not logged in" }, statusCode: 401);
        }

        user.FplTeamId = request.FplTeamId;
        await userManager.UpdateAsync(user);

        return Results.Ok(UserResponse.From(user));
    }
}
