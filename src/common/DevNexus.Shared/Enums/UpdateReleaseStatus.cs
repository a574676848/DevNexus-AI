namespace DevNexus.Shared.Enums;

/// <summary>
/// 发布版本状态。
/// </summary>
public enum UpdateReleaseStatus
{
    /// <summary>
    /// 草稿。
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已发布。
    /// </summary>
    Published = 1,

    /// <summary>
    /// 已归档。
    /// </summary>
    Archived = 2
}

/// <summary>
/// 发布版本状态字符串协议扩展。
/// </summary>
public static class UpdateReleaseStatusExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this UpdateReleaseStatus status)
    {
        return status switch
        {
            UpdateReleaseStatus.Published => "published",
            UpdateReleaseStatus.Archived => "archived",
            _ => "draft"
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static UpdateReleaseStatus Parse(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "published" => UpdateReleaseStatus.Published,
            "archived" => UpdateReleaseStatus.Archived,
            _ => UpdateReleaseStatus.Draft
        };
    }
}