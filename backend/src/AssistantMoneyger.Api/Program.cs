using AssistantMoneyger.Api.Data;
using AssistantMoneyger.Api.Features.Auth;
using AssistantMoneyger.Api.Features.Evaluation;
using AssistantMoneyger.Api.Features.Gameweek;
using AssistantMoneyger.Api.Features.Predictions;
using AssistantMoneyger.Api.Features.Squad;
using AssistantMoneyger.Api.Features.Team;
using AssistantMoneyger.Api.Fpl;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        // The frontend's RegisterView only ever promised "8+ characters" —
        // Identity's defaults also require upper/lower/digit/symbol, which
        // would silently reject passwords the UI told the user were fine.
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;

    // Identity's default behaviour for an API with no session is to 302
    // redirect to a login *page* — that's built for server-rendered apps,
    // not a fetch() caller expecting JSON. Short-circuit to plain status
    // codes instead.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<FplApiClient>(client =>
{
    client.BaseAddress = new Uri("https://fantasy.premierleague.com/api/");
    // bootstrap-static (the full ~650-player list) is a genuinely large
    // payload — 15s was enough for the response headers but not always
    // enough to finish streaming and deserializing the body.
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Cheap second line of defence against cross-site form/fetch submissions,
// on top of the SameSite=Lax cookie: reject any state-changing request
// whose browser-sent Origin doesn't match the frontend's real origin.
//
// This is compared against a configured value, NOT reconstructed from
// Request.Scheme/Request.Host — behind Vite's dev proxy (and behind any
// real reverse proxy in production), the backend sees the proxy's own
// host on that hop, not the browser's actual origin, so reconstructing
// "expected" from the request would reject every legitimate request.
var allowedOrigin = builder.Configuration["AllowedOrigin"];
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    if (method is "POST" or "PUT" or "DELETE")
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && !string.IsNullOrEmpty(allowedOrigin)
            && !string.Equals(origin, allowedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();
app.MapTeamEndpoints();
app.MapGameweekEndpoints();
app.MapPredictionsEndpoints();
app.MapEvaluationEndpoints();
app.MapSquadEndpoints();

app.Run();

// Needed so AssistantMoneyger.Api.Tests can reference the entry point's
// assembly (e.g. for WebApplicationFactory-style tests later).
public partial class Program;
