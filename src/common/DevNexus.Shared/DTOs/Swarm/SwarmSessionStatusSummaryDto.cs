namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// Swarm 会话状态摘要 DTO。
/// </summary>
public record SwarmSessionStatusSummaryDto
{
    /// <summary>
    /// 主状态视觉语义。
    /// </summary>
    public string Tone { get; init; } = "neutral";

    /// <summary>
    /// 主状态标题。
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// 主状态说明。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 工作包总数。
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 规划阶段工作包数量。
    /// </summary>
    public int PlanningCount { get; init; }

    /// <summary>
    /// 执行阶段工作包数量。
    /// </summary>
    public int ExecutingCount { get; init; }

    /// <summary>
    /// 评估阶段工作包数量。
    /// </summary>
    public int EvaluatingCount { get; init; }

    /// <summary>
    /// 终态工作包数量。
    /// </summary>
    public int TerminalCount { get; init; }

    /// <summary>
    /// 失败工作包数量。
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// 是否存在失败工作包。
    /// </summary>
    public bool HasFailures { get; init; }

    /// <summary>
    /// 会话是否暂停。
    /// </summary>
    public bool IsPaused { get; init; }

    /// <summary>
    /// 会话是否已进入终态。
    /// </summary>
    public bool IsTerminal { get; init; }

    /// <summary>
    /// 阶段指标列表。
    /// </summary>
    public List<SwarmStageMetricDto> StageMetrics { get; init; } = new();
}
