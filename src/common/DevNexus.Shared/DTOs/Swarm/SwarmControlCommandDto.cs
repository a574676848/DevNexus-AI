namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// Swarm 控制命令结果。
/// </summary>
public sealed class SwarmControlCommandDto
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 控制命令。
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 控制命令是否被接受。
    /// </summary>
    public bool Accepted { get; set; } = true;

    /// <summary>
    /// 控制命令结果说明。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 控制命令生效后的状态摘要。
    /// </summary>
    public SwarmSessionStatusSummaryDto? StatusSummary { get; set; }
}
