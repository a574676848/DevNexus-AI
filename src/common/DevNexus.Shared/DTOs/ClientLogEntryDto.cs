namespace DevNexus.Shared.DTOs;

/// <summary>
/// 客户端日志条目 DTO
/// 用于客户端到服务端的日志传输
/// </summary>
public class ClientLogEntryDto
{
    /// <summary>
    /// 日志时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// 日志级别 (Trace, Debug, Information, Warning, Error, Critical)
    /// </summary>
    public string Level { get; set; } = "Error";

    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 异常完整信息（含堆栈）
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// 异常来源（如 UnhandledException, ErrorBoundary, ApiService）
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 用户 ID（可选，未登录时为 null）
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 客户端版本
    /// </summary>
    public string? ClientVersion { get; set; }

    /// <summary>
    /// 平台（Windows, Android, iOS）
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// 设备型号
    /// </summary>
    public string? DeviceModel { get; set; }

    /// <summary>
    /// 操作系统版本
    /// </summary>
    public string? OsVersion { get; set; }

    /// <summary>
    /// 附加数据（键值对）
    /// </summary>
    public Dictionary<string, object?>? AdditionalData { get; set; }
}

