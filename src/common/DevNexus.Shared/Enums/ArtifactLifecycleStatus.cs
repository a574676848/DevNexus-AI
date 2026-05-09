namespace DevNexus.Shared.Enums;

/// <summary>
/// Artifact 生命周期状态。
/// </summary>
public enum ArtifactLifecycleStatus
{
    /// <summary>
    /// 未知状态。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 草稿。
    /// </summary>
    Draft = 1,

    /// <summary>
    /// 已激活。
    /// </summary>
    Active = 2,

    /// <summary>
    /// 已归档。
    /// </summary>
    Archived = 3,

    /// <summary>
    /// 已删除。
    /// </summary>
    Deleted = 4
}

/// <summary>
/// Artifact 生命周期状态字符串协议扩展。
/// </summary>
public static class ArtifactLifecycleStatusExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this ArtifactLifecycleStatus status)
    {
        return status switch
        {
            ArtifactLifecycleStatus.Draft => "draft",
            ArtifactLifecycleStatus.Archived => "archived",
            ArtifactLifecycleStatus.Deleted => "deleted",
            ArtifactLifecycleStatus.Active => "active",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static ArtifactLifecycleStatus Parse(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "draft" => ArtifactLifecycleStatus.Draft,
            "active" => ArtifactLifecycleStatus.Active,
            "archived" => ArtifactLifecycleStatus.Archived,
            "deleted" => ArtifactLifecycleStatus.Deleted,
            _ => ArtifactLifecycleStatus.Unknown
        };
    }
}