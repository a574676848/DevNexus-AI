using DevNexus.Domain.Enums;

namespace DevNexus.Domain.Models.Swarm;

/// <summary>
/// 表示一个可独立闭环执行的上下文工作包。
/// </summary>
public class ContextWorkPackage
{
    /// <summary>
    /// 工作包唯一标识。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 所属会话 ID。
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 工作包标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 工作包目标。
    /// </summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>
    /// 上下文类型。
    /// </summary>
    public SwarmContextType ContextType { get; set; } = SwarmContextType.Unknown;

    /// <summary>
    /// 任务上下文。
    /// </summary>
    public string TaskContext { get; set; } = string.Empty;

    /// <summary>
    /// 状态上下文。
    /// </summary>
    public string StateContext { get; set; } = string.Empty;

    /// <summary>
    /// 记忆上下文。
    /// </summary>
    public string MemoryContext { get; set; } = string.Empty;

    /// <summary>
    /// 证据上下文。
    /// </summary>
    public string EvidenceContext { get; set; } = string.Empty;

    /// <summary>
    /// 输入契约列表。
    /// </summary>
    public List<ContextContract> InputContracts { get; set; } = new();

    /// <summary>
    /// 输出契约列表。
    /// </summary>
    public List<ContextContract> OutputContracts { get; set; } = new();

    /// <summary>
    /// 上下文依赖列表。
    /// </summary>
    public List<ContextDependency> Dependencies { get; set; } = new();

    /// <summary>
    /// 可见性级别。
    /// </summary>
    public SwarmVisibilityLevel VisibilityLevel { get; set; } = SwarmVisibilityLevel.DependencyScoped;

    /// <summary>
    /// 执行策略。
    /// </summary>
    public SwarmExecutionStrategy ExecutionStrategy { get; set; } = SwarmExecutionStrategy.SingleAgentSequential;

    /// <summary>
    /// 风险等级，范围 0 到 10。
    /// </summary>
    public double RiskLevel { get; set; }

    /// <summary>
    /// 是否允许并行执行。
    /// </summary>
    public bool CanRunInParallel { get; set; }

    /// <summary>
    /// 当前工作包拥有的文件范围。
    /// </summary>
    public List<string> OwnedFiles { get; set; } = new();

    /// <summary>
    /// 当前工作包拥有的符号范围。
    /// </summary>
    public List<string> OwnedSymbols { get; set; } = new();

    /// <summary>
    /// 工作包状态。
    /// </summary>
    public SwarmPackageStatus Status { get; set; } = SwarmPackageStatus.Pending;

    /// <summary>
    /// 工作包结果摘要。
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 工作包评估摘要。
    /// </summary>
    public string? Evaluation { get; set; }

    /// <summary>
    /// 最近失败原因。
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 最近执行体名称。
    /// </summary>
    public string? ExecutorName { get; set; }

    /// <summary>
    /// 最近命令行摘要。
    /// </summary>
    public string? CommandLine { get; set; }

    /// <summary>
    /// 最近工作目录。
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 最近执行报告 Artifact 标识。
    /// </summary>
    public Guid? ExecutionReportArtifactId { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
