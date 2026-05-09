using DevNexus.ApiService.Auth;
using DevNexus.Domain.Configuration;
using DevNexus.Shared.Constants;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace DevNexus.ApiService.Middlewares;

/// <summary>
/// 监控中间件 - 记录请求性能和指标
/// </summary>
public class MonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MonitoringMiddleware> _logger;
    private readonly MonitoringOptions _options;

    public MonitoringMiddleware(
        RequestDelegate next,
        ILogger<MonitoringMiddleware> logger,
        IOptions<MonitoringOptions> options)
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
        if (!_options.EnableDetailedMonitoring)
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var spanId = Activity.Current?.SpanId.ToString() ?? "0000000000000000";
        var requestId = Guid.NewGuid().ToString("N");

        // 添加请求 ID 到响应头
        context.Response.Headers["X-Request-Id"] = requestId;
        context.Response.Headers["X-Trace-Id"] = traceId;

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var authInfo = AuthenticatedRequestInfoResolver.Resolve(context.User);

            // 记录请求指标
            var duration = sw.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;
            var method = context.Request.Method;
            var path = context.Request.Path;
            var userId = authInfo.UserId?.ToString() ?? "anonymous";
            var roleBucket = authInfo.IsAdmin ? RoleNames.Admin : authInfo.IsAuthenticated ? RoleNames.User : "Anonymous";

            // 仅在非成功请求或开启了详细监控时记录 Information
            // 这里我们保持 Information 但合并字段，符合结构化日志标准
            if (statusCode >= 400 || duration > _options.SlowQueryThresholdMs)
            {
                var level = statusCode >= 500 ? LogLevel.Error : LogLevel.Warning;
                _logger.Log(level,
                    "[Request.Audit] {Method} {Path} | Status={StatusCode} | Duration={Duration}ms | User={User} | RoleBucket={RoleBucket} | TraceId={TraceId} | SpanId={SpanId}",
                    method, path, statusCode, duration, userId, roleBucket, traceId, spanId);
            }
            else
            {
                // 成功的常规请求记录为 Debug，减少日志噪音
                _logger.LogDebug(
                    "[Request.Completed] {Method} {Path} | Status={StatusCode} | Duration={Duration}ms | User={User} | RoleBucket={RoleBucket} | TraceId={TraceId}",
                    method, path, statusCode, duration, userId, roleBucket, traceId);
            }
        }
    }
}
