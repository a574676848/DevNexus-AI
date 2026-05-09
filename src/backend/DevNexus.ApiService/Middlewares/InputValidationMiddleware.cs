using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Middlewares;

/// <summary>
/// 输入验证中间件 - 提供额外的安全验证层
/// </summary>
public class InputValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputValidationMiddleware> _logger;

    // 危险模式列表（用于检测潜在的注入攻击）
    private static readonly string[] DangerousPatterns = new[]
    {
        "<script", "javascript:", "onerror=", "onload=",  // XSS
        "'; DROP TABLE", "'; DELETE FROM", "UNION SELECT", // SQL Injection
        "../", "..\\",  // Path Traversal
        "eval(", "exec(", "system(", "shell_exec(",  // Code Injection
        "${", "{{",  // Template Injection
    };

    public InputValidationMiddleware(
        RequestDelegate next,
        ILogger<InputValidationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 处理请求
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var isExcludedPath = path.Contains("/api/v1/artifact");

        // 只验证 POST/PUT/PATCH 请求
        if (context.Request.Method is "POST" or "PUT" or "PATCH")
        {
            // 验证 Content-Type
            if (!IsValidContentType(context.Request.ContentType))
            {
                await RejectRequest(context, "Invalid Content-Type");
                return;
            }

            // 验证请求体大小（最大 10MB）
            if (context.Request.ContentLength > 10 * 1024 * 1024)
            {
                await RejectRequest(context, "Request body too large (max 10MB)");
                return;
            }

            // 跳过 multipart/form-data 的请求体检测（文件上传）
            // 二进制文件内容不应进行文本模式检测
            var isMultipart = context.Request.ContentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;
            
            if (!isMultipart && !isExcludedPath)
            {
                // 读取并验证请求体（仅非文件上传请求且非排除路径）
                context.Request.EnableBuffering();
                var body = await ReadRequestBodyAsync(context.Request);

                if (!string.IsNullOrEmpty(body))
                {
                    // 检测危险模式
                    var dangerousPattern = DetectDangerousPattern(body);
                    if (dangerousPattern != null)
                    {
                        _logger.LogWarning(
                            "[InputValidation.Rejected] Dangerous pattern detected | Pattern={Pattern} | Path={Path} | IP={IP}",
                            dangerousPattern, context.Request.Path, context.Connection.RemoteIpAddress);

                        await RejectRequest(context, "Invalid input detected");
                        return;
                    }

                    // 验证 JSON 格式（如果是 JSON）
                    if (context.Request.ContentType?.Contains("application/json") == true)
                    {
                        if (!IsValidJson(body))
                        {
                            await RejectRequest(context, "Invalid JSON format");
                            return;
                        }
                    }
                }

                // 重置流位置以便后续读取
                context.Request.Body.Position = 0;
            }
        }

        // 验证查询参数（排除路径仍然验证查询参数，因为查询参数通常较短且不应包含复杂代码）
        foreach (var param in context.Request.Query)
        {
            var dangerousPattern = DetectDangerousPattern(param.Value.ToString());
            if (dangerousPattern != null)
            {
                _logger.LogWarning(
                    "[InputValidation.Rejected] Dangerous pattern in query | Pattern={Pattern} | Param={Param} | Path={Path}",
                    dangerousPattern, param.Key, context.Request.Path);

                await RejectRequest(context, "Invalid query parameter");
                return;
            }
        }

        await _next(context);
    }

    /// <summary>
    /// 验证 Content-Type 是否有效
    /// </summary>
    private bool IsValidContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return true; // 允许空 Content-Type（GET 请求）
        }

        var validTypes = new[]
        {
            "application/json",
            "application/x-www-form-urlencoded",
            "multipart/form-data",
            "text/plain"
        };

        return validTypes.Any(t => contentType.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 读取请求体
    /// </summary>
    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        try
        {
            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InputValidation.Error] Failed to read request body");
            return string.Empty;
        }
    }

    /// <summary>
    /// 检测危险模式
    /// </summary>
    private string? DetectDangerousPattern(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        var lowerInput = input.ToLowerInvariant();
        return DangerousPatterns.FirstOrDefault(pattern =>
            lowerInput.Contains(pattern.ToLowerInvariant()));
    }

    /// <summary>
    /// 验证 JSON 格式
    /// </summary>
    private bool IsValidJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 拒绝请求
    /// </summary>
    private async Task RejectRequest(HttpContext context, string reason)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid Request",
            Detail = reason,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
