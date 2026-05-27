using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Configuration;

/// <summary>
/// Agent 自动化配置。
/// </summary>
public sealed class AgentAutomationOptions
{
    /// <summary>
    /// 默认审批模式。
    /// </summary>
    public AgentApprovalMode DefaultApprovalMode { get; set; } = AgentApprovalMode.AskUser;

}
