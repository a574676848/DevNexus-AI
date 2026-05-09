namespace DevNexus.Shared.DTOs;

/// <summary>
/// 统一 API 响应
/// </summary>
public class ApiResponse<T>
{
    /// <summary>
    /// 业务状态码
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 响应数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 错误列表
    /// </summary>
    public List<ApiErrorDetail> Errors { get; set; } = new();

    /// <summary>
    /// 响应时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ApiResponse<T> Success(T data, string message = "Success", int code = 200)
    {
        return new ApiResponse<T>
        {
            Code = code,
            Message = message,
            Data = data,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static ApiResponse<T> Error(string message, int code, string? field = null)
    {
        var response = new ApiResponse<T>
        {
            Code = code,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        response.Errors.Add(new ApiErrorDetail
        {
            Field = field,
            Message = message
        });

        return response;
    }
}

/// <summary>
/// API 错误详情
/// </summary>
public class ApiErrorDetail
{
    /// <summary>
    /// 关联字段
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}