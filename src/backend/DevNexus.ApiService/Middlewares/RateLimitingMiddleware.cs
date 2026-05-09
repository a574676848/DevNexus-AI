using DevNexus.ApiService.Auth;
using DevNexus.Domain.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DevNexus.ApiService.Middlewares;

/// <summary>
/// API 速率限制中间件
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitOptions _options;
    private static readonly ConcurrentDictionary<string, RateLimitCounter> _counters = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<RateLimitOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 处理请求
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // 确定速率限制策略
        var policy = DeterminePolicy(context);
        if (policy == null)
        {
            await _next(context);
            return;
        }

        // 获取客户端标识
        var clientId = GetClientIdentifier(context);
        var key = $"{clientId}:{context.Request.Path}";

        // 检查速率限制
        var counter = _counters.GetOrAdd(key, _ => new RateLimitCounter());

        lock (counter)
        {
            // 清理过期计数
            counter.CleanupExpired(policy.Window);

            // 检查是否超过限制
            if (counter.Count >= policy.PermitLimit)
            {
                _logger.LogWarning(
                    "[RateLimit.Exceeded] Client exceeded rate limit | ClientId={ClientId} | Path={Path} | Limit={Limit}",
                    clientId, context.Request.Path, policy.PermitLimit);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = policy.Window.TotalSeconds.ToString();
                context.Response.Headers["X-RateLimit-Limit"] = policy.PermitLimit.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = "0";
                context.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.Add(policy.Window).ToUnixTimeSeconds().ToString();

                return;
            }

            // 增加计数
            counter.Increment();

            // 设置响应头
            context.Response.Headers["X-RateLimit-Limit"] = policy.PermitLimit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (policy.PermitLimit - counter.Count).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.Add(policy.Window).ToUnixTimeSeconds().ToString();
        }

        await _next(context);
    }

    /// <summary>
    /// 确定适用的速率限制策略
    /// </summary>
    private RateLimitPolicy? DeterminePolicy(HttpContext context)
    {
        var authInfo = AuthenticatedRequestInfoResolver.Resolve(context.User);
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // 代码执行接口
        if (path.Contains("/api/code/execute") || path.Contains("/api/script/run"))
        {
            return _options.CodeExecution;
        }

        // AI 聊天接口
        if (path.Contains("/chat-hub") || path.Contains("/api/chat"))
        {
            return _options.Chat;
        }

        // 认证用户
        if (authInfo.IsAuthenticated)
        {
            return _options.Authenticated;
        }

        // 全局限制
        return _options.Global;
    }

    /// <summary>
    /// 获取客户端标识符
    /// </summary>
    private string GetClientIdentifier(HttpContext context)
    {
        var authInfo = AuthenticatedRequestInfoResolver.Resolve(context.User);
        if (authInfo.IsAuthenticated)
        {
            return authInfo.UserKey;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }
}

/// <summary>
/// 速率限制计数器
/// </summary>
internal class RateLimitCounter
{
    private readonly List<DateTimeOffset> _requests = new();

    public int Count => _requests.Count;

    public void Increment()
    {
        _requests.Add(DateTimeOffset.UtcNow);
    }

    public void CleanupExpired(TimeSpan window)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(window);
        _requests.RemoveAll(r => r < cutoff);
    }
}
