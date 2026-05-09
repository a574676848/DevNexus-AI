namespace DevNexus.Shared.Enums;

/// <summary>
/// 客户端更新事件结果。
/// </summary>
public enum UpdateClientEventResult
{
    /// <summary>
    /// 未知结果。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 成功。
    /// </summary>
    Success = 1,

    /// <summary>
    /// 失败。
    /// </summary>
    Failed = 2
}

/// <summary>
/// 客户端更新事件结果字符串协议扩展。
/// </summary>
public static class UpdateClientEventResultExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this UpdateClientEventResult result)
    {
        return result switch
        {
            UpdateClientEventResult.Failed => "failed",
            UpdateClientEventResult.Success => "success",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static UpdateClientEventResult Parse(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "failed" => UpdateClientEventResult.Failed,
            "success" => UpdateClientEventResult.Success,
            _ => UpdateClientEventResult.Unknown
        };
    }
}