namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// Swarm 工作包快照 DTO。
/// </summary>
public record ContextSwarmPackageSnapshotDto
{
    /// <summary>
    /// 会话 ID。
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// 工作包列表。
    /// </summary>
    public List<ContextWorkPackageDto> Packages { get; init; } = new();

    /// <summary>
    /// 工作包数量。
    /// </summary>
    public int PackageCount { get; init; }

    /// <summary>
    /// 会话状态摘要。
    /// </summary>
    public SwarmSessionStatusSummaryDto StatusSummary { get; init; } = new();
}
