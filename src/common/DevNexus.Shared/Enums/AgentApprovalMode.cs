namespace DevNexus.Shared.Enums;

/// <summary>
/// Agent 自动化审批模式。
/// </summary>
public enum AgentApprovalMode
{
    /// <summary>
    /// 只建议，不自动执行高风险操作。
    /// </summary>
    Suggest = 0,

    /// <summary>
    /// 允许自动编辑，写操作仍需按风险审批。
    /// </summary>
    AutoEdit = 1,

    /// <summary>
    /// 全自动模式，高风险操作仍保留系统级保护。
    /// </summary>
    FullAuto = 2
}
