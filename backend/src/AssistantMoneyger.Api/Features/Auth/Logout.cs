using AssistantMoneyger.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace AssistantMoneyger.Api.Features.Auth;

public static class Logout
{
    public static async Task<IResult> Handle(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }
}
