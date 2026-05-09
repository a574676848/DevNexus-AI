namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// 上下文工作包 DTO。
/// </summary>
public record ContextWorkPackageDto
{
    /// <summary>
    /// 工作包唯一标识。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 工作包标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 工作包目标。
    /// </summary>
    public string Objective { get; init; } = string.Empty;

    /// <summary>
    /// 上下文类型。
    /// </summary>
    public string ContextType { get; init; } = string.Empty;

    /// <summary>
    /// 当前状态。
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 执行策略。
    /// </summary>
    public string ExecutionStrategy { get; init; } = string.Empty;

    /// <summary>
    /// 依赖工作包 ID 列表。
    /// </summary>
    public List<string> Dependencies { get; init; } = new();

    /// <summary>
    /// 结果摘要。
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// 最近失败原因。
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 风险等级。
    /// </summary>
    public double RiskLevel { get; init; }

    /// <summary>
    /// 最近执行体名称。
    /// </summary>
    public string? ExecutorName { get; init; }

    /// <summary>
    /// 最近命令行摘要。
    /// </summary>
    public string? CommandLine { get; init; }

    /// <summary>
    /// 最近工作目录。
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// 执行报告 Artifact 标识。
    /// </summary>
    public Guid? ExecutionReportArtifactId { get; init; }

    /// <summary>
    /// 最近更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 是否允许重试。
    /// </summary>
    public bool CanRetry { get; init; }

    /// <summary>
    /// 该工作包拥有的文件范围。
    /// </summary>
    public List<string> OwnedFiles { get; init; } = new();

    /// <summary>
    /// 该工作包拥有的符号范围。
    /// </summary>
    public List<string> OwnedSymbols { get; init; } = new();
}
