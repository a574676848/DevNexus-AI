using DevNexus.Core.Abstractions;
using DevNexus.ApiService.Auth;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// Authenticated controller base that exposes the current user context.
/// </summary>
public abstract class AuthenticatedControllerBase : ControllerBase
{
    protected AuthenticatedControllerBase(IUserContextAccessor userContextAccessor)
    {
    }

    private AuthenticatedRequestInfo CurrentRequest => AuthenticatedRequestInfoResolver.Resolve(User);
    protected Guid? CurrentUserId => CurrentRequest.UserId;

    protected bool TryGetCurrentUserId(out Guid userId)
    {
        if (CurrentRequest.UserId.HasValue)
        {
            userId = CurrentRequest.UserId.Value;
            return true;
        }

        userId = Guid.Empty;
        return false;
    }

    protected Guid RequireCurrentUserId()
    {
        if (!CurrentRequest.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("用户未认证");
        }

        return CurrentRequest.UserId.Value;
    }

    protected bool IsAdmin()
    {
        return CurrentRequest.IsAdmin;
    }
}
