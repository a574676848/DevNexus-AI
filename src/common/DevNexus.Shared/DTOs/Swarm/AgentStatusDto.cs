namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// 智能体状态 DTO。
/// </summary>
public record AgentStatusDto
{
    /// <summary>
    /// 智能体名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 智能体角色。
    /// </summary>
    public string Role { get; init; } = "Assistant";

    /// <summary>
    /// 当前状态。
    /// </summary>
    public string Status { get; init; } = "Idle";

    /// <summary>
    /// 当前动作说明。
    /// </summary>
    public string CurrentAction { get; init; } = string.Empty;
}
