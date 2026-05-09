namespace DevNexus.Core.Services.Swarm.Execution;

/// <summary>
/// Swarm 任务执行结果。
/// </summary>
public sealed class SwarmTaskExecutionResult
{
    /// <summary>
    /// 任务最终输出内容。
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 执行通道类型。
    /// </summary>
    public string ExecutionKind { get; init; } = "LlmAgent";

    /// <summary>
    /// 实际执行体名称。
    /// </summary>
    public string ExecutorName { get; init; } = string.Empty;

    /// <summary>
    /// 是否执行成功。
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// 最近失败原因。
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 关联的执行报告 Artifact。
    /// </summary>
    public Guid? ArtifactId { get; init; }

    /// <summary>
    /// 结构化元数据。
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
