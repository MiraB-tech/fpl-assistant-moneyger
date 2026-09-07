namespace AssistantMoneyger.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", Register.Handle);
        group.MapPost("/login", Login.Handle);
        group.MapPost("/logout", Logout.Handle).RequireAuthorization();
        group.MapGet("/me", Me.Handle).RequireAuthorization();

        return app;
    }
}
