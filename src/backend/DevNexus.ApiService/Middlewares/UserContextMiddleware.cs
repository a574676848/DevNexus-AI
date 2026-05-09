using DevNexus.ApiService.Auth;

namespace DevNexus.ApiService.Middlewares;

/// <summary>
/// 请求级用户上下文中间件
/// </summary>
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserContextMiddleware> _logger;

    public UserContextMiddleware(
        RequestDelegate next,
        ILogger<UserContextMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 将已认证用户写入异步上下文，供 HostService 等单例服务读取
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IUserContextAccessor userContextAccessor)
    {
        if (AuthenticatedUserResolver.TryGetUserId(context.User, out var userId))
        {
            userContextAccessor.CurrentUserId = userId;
            userContextAccessor.CurrentSessionId = null;
            userContextAccessor.CurrentConnectionId = null;
        }
        else if (context.User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogWarning(
                "[UserContext] 无法解析用户声明中的用户ID | Path={Path}",
                context.Request.Path);
        }

        try
        {
            await _next(context);
        }
        finally
        {
            // IUserContextAccessor 基于 AsyncLocal + Singleton，必须在请求结束后清理，避免脏上下文泄漏。
            userContextAccessor.CurrentUserId = null;
            userContextAccessor.CurrentSessionId = null;
            userContextAccessor.CurrentConnectionId = null;
        }
    }
}
