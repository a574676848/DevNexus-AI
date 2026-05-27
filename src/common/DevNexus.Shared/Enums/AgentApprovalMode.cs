namespace DevNexus.Shared.Enums;

/// <summary>
/// Agent 自动化审批模式。
/// </summary>
public enum AgentApprovalMode
{
    /// <summary>
    /// 所有策略命中的操作都询问用户。
    /// </summary>
    AskUser = 0,

    /// <summary>
    /// 由 Agent 自主执行中风险操作，高风险操作仍询问用户。
    /// </summary>
    AgentDecides = 1,

    /// <summary>
    /// 完全放权给 Agent 执行。
    /// </summary>
    FullAccess = 2
}
