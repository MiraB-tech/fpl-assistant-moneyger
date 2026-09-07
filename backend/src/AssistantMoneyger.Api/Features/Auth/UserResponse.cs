using System.Text.Json.Serialization;
using AssistantMoneyger.Api.Data;

namespace AssistantMoneyger.Api.Features.Auth;

// Matches the frontend's User type in frontend/src/types.ts exactly.
public record UserResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("fpl_team_id")] int? FplTeamId)
{
    public static UserResponse From(ApplicationUser user) =>
        new(user.Id, user.Email ?? "", user.FplTeamId);
}
