using Microsoft.AspNetCore.Identity;

namespace AssistantMoneyger.Api.Data;

// Extends Identity's built-in user with the one extra field this app
// needs: which FPL team this account is tracking. IdentityUser<int>
// (rather than the default string/GUID key) keeps User.id a plain
// number in JSON, matching the frontend's existing User type.
public class ApplicationUser : IdentityUser<int>
{
    public int? FplTeamId { get; set; }
}
