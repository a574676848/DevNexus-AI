namespace DevNexus.Domain.Models.Swarm;

/// <summary>
/// 表示一次基于上下文工作包的 Swarm 规划结果。
/// </summary>
public class SwarmExecutionPlan
{
    /// <summary>
    /// 会话 ID。
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 规划摘要。
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 工作包列表。
    /// </summary>
    public List<ContextWorkPackage> Packages { get; set; } = new();
}
