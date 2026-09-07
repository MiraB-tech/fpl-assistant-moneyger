using System.Security.Claims;
using AssistantMoneyger.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace AssistantMoneyger.Api.Features.Auth;

public static class Me
{
    public static async Task<IResult> Handle(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        return user is null
            ? Results.Json(new { error = "not logged in" }, statusCode: 401)
            : Results.Ok(UserResponse.From(user));
    }
}
