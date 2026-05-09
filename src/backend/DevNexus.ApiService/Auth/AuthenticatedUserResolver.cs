using System.Security.Claims;

namespace DevNexus.ApiService.Auth;

internal static class AuthenticatedUserResolver
{
    public static bool TryGetUserId(ClaimsPrincipal? principal, out Guid userId)
    {
        userId = Guid.Empty;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var claimValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? principal.FindFirst("sub")?.Value;

        return !string.IsNullOrWhiteSpace(claimValue)
            && Guid.TryParse(claimValue, out userId);
    }
}
