namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// Swarm 阶段指标 DTO。
/// </summary>
public record SwarmStageMetricDto
{
    /// <summary>
    /// 阶段显示名称。
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// 阶段内工作包数量。
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// 当前阶段是否为主状态。
    /// </summary>
    public bool Active { get; init; }

    /// <summary>
    /// 阶段视觉语义。
    /// </summary>
    public string Tone { get; init; } = string.Empty;
}
