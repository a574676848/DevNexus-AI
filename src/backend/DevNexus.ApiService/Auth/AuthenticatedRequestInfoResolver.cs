using DevNexus.Shared.Constants;
using System.Security.Claims;

namespace DevNexus.ApiService.Auth;

internal static class AuthenticatedRequestInfoResolver
{
    public static AuthenticatedRequestInfo Resolve(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return AuthenticatedRequestInfo.Anonymous;
        }

        var isAdmin = principal.IsInRole(RoleNames.Admin);
        var userId = AuthenticatedUserResolver.TryGetUserId(principal, out var parsedUserId)
            ? parsedUserId
            : (Guid?)null;

        var userKey = userId.HasValue ? $"user:{userId.Value}" : "authenticated:unknown";

        return new AuthenticatedRequestInfo(true, userId, isAdmin, userKey);
    }
}
