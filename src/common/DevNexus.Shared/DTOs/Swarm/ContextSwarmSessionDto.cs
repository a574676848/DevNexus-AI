namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// 基于上下文工作包的 Swarm 会话 DTO。
/// </summary>
public record ContextSwarmSessionDto
{
    /// <summary>
    /// 会话 ID。
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// 会话标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 会话状态。
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 工作包列表。
    /// </summary>
    public List<ContextWorkPackageDto> Packages { get; init; } = new();

    /// <summary>
    /// 会话状态摘要。
    /// </summary>
    public SwarmSessionStatusSummaryDto? StatusSummary { get; init; }
}
