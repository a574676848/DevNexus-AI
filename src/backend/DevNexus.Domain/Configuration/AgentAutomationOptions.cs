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
    public AgentApprovalMode DefaultApprovalMode { get; set; } = AgentApprovalMode.Suggest;

    /// <summary>
    /// 是否允许全自动模式绕过中风险操作确认。
    /// </summary>
    public bool AllowFullAutoForMediumRisk { get; set; }

    /// <summary>
    /// 是否始终保护高风险操作。
    /// </summary>
    public bool AlwaysProtectHighRisk { get; set; } = true;
}
