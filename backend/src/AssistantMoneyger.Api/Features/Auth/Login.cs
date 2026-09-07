using AssistantMoneyger.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace AssistantMoneyger.Api.Features.Auth;

public record LoginRequest(string Email, string Password);

public static class Login
{
    public static async Task<IResult> Handle(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Json(new { error = "invalid email or password" }, statusCode: 401);
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return Results.Json(new { error = "invalid email or password" }, statusCode: 401);
        }

        return Results.Ok(UserResponse.From(user));
    }
}
