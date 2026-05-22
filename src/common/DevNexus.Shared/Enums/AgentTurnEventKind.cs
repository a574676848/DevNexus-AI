namespace DevNexus.Shared.Enums;

/// <summary>
/// Agent 单轮事件类型。
/// </summary>
public enum AgentTurnEventKind
{
    /// <summary>
    /// 工具执行完成。
    /// </summary>
    ToolCompleted = 1,

    /// <summary>
    /// 工具执行失败。
    /// </summary>
    ToolFailed = 2
}
