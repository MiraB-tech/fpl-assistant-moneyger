using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Fpl;
using Microsoft.AspNetCore.Identity;

namespace AssistantMoneyger.Api.Features.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    [property: JsonPropertyName("fpl_team_id")] int? FplTeamId);

public static class Register
{
    public static async Task<IResult> Handle(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        FplApiClient fpl)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            return Results.BadRequest(new { error = "a valid email is required" });
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
        {
            return Results.BadRequest(new { error = "password must be at least 8 characters" });
        }

        if (request.FplTeamId is int teamId)
        {
            try
            {
                await fpl.GetEntryAsync(teamId);
            }
            catch (HttpRequestException)
            {
                return Results.BadRequest(new { error = "that FPL team ID doesn't seem to exist" });
            }
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FplTeamId = request.FplTeamId,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var message = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return Results.BadRequest(new { error = message });
        }

        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Created("/api/auth/me", UserResponse.From(user));
    }
}
