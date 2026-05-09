namespace DevNexus.Client.Shared.Services.Exceptions;

/// <summary>
/// API 异常
/// </summary>
public class ApiException : Exception
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// 响应内容
    /// </summary>
    public string? ResponseContent { get; }

    /// <summary>
    /// 原始错误数据（ResponseContent 的别名，便于前端调用）
    /// </summary>
    public string? RawErrorData => ResponseContent;

    /// <summary>
    /// 追踪ID
    /// </summary>
    public string? TraceId { get; }

    public ApiException(string message) : base(message)
    {
        StatusCode = 0;
    }

    public ApiException(string message, int statusCode, string? responseContent = null) 
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
        
        // 尝试解析 TraceId
        if (!string.IsNullOrEmpty(responseContent))
        {
            try
            {
                // 简单解析 ProblemDetails 格式
                if (responseContent.Contains("traceId"))
                {
                    var start = responseContent.IndexOf("\"traceId\":");
                    if (start > 0)
                    {
                        var end = responseContent.IndexOf(",", start);
                        if (end < 0) end = responseContent.IndexOf("}", start);
                        if (end > start)
                        {
                            TraceId = responseContent.Substring(start + 11, end - start - 12).Trim('"');
                        }
                    }
                }
            }
            catch
            {
                // 解析失败忽略
            }
        }
    }

    public ApiException(string message, Exception innerException) 
        : base(message, innerException)
    {
        StatusCode = 0;
    }

    /// <summary>
    /// 是否为认证错误
    /// </summary>
    public bool IsAuthenticationError => StatusCode == 401;

    /// <summary>
    /// 是否为授权错误
    /// </summary>
    public bool IsAuthorizationError => StatusCode == 403;

    /// <summary>
    /// 是否为服务器错误
    /// </summary>
    public bool IsServerError => StatusCode >= 500;

    /// <summary>
    /// 获取用户友好的错误消息
    /// </summary>
    public string GetUserFriendlyMessage()
    {
        return StatusCode switch
        {
            400 => "请求参数有误，请检查输入",
            401 => "登录已过期，请重新登录",
            403 => "没有权限执行此操作",
            404 => "请求的资源不存在",
            429 => "请求过于频繁，请稍后再试",
            500 => "服务器内部错误，请稍后重试",
            502 or 503 or 504 => "服务暂时不可用，请稍后重试",
            _ => Message
        };
    }
}
