using System.Security.Claims;

namespace RaceDay.Api.Helpers;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Returns the id of the currently authenticated user from the session claims.</summary>
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException("NameIdentifier claim missing."));
}
