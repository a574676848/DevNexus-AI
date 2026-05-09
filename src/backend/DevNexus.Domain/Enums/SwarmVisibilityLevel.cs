namespace DevNexus.Domain.Enums;

/// <summary>
/// 上下文可见性级别。
/// </summary>
public enum SwarmVisibilityLevel
{
    /// <summary>
    /// 仅当前工作包可见。
    /// </summary>
    PackageOnly = 0,

    /// <summary>
    /// 当前工作包及其直接依赖可见。
    /// </summary>
    DependencyScoped = 1,

    /// <summary>
    /// 会话内可见。
    /// </summary>
    SessionWide = 2
}
