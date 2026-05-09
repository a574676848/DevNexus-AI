namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// Swarm 会话 DTO。
/// </summary>
public record SwarmSessionDto
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
    /// 会话描述。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 会话状态。
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 开始时间。
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// 完成时间。
    /// </summary>
    public DateTime? CompletedAt { get; init; }
}
