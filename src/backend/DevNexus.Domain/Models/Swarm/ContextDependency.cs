namespace DevNexus.Domain.Models.Swarm;

/// <summary>
/// 表示上下文工作包之间的依赖关系。
/// </summary>
public class ContextDependency
{
    /// <summary>
    /// 上游工作包 ID。
    /// </summary>
    public string SourcePackageId { get; set; } = string.Empty;

    /// <summary>
    /// 下游工作包 ID。
    /// </summary>
    public string TargetPackageId { get; set; } = string.Empty;

    /// <summary>
    /// 依赖原因。
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
