using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 挂起交互解决策略。
/// </summary>
public static class PendingInteractionResolutionPolicy
{
    /// <summary>
    /// 将外部动作归一为稳定协议动作。
    /// </summary>
    public static PendingInteractionResolutionDecision Resolve(string? action)
    {
        return action?.Trim().ToLowerInvariant() switch
        {
            PendingInteractionResolutionActions.Approve
                or PendingInteractionResolutionActions.ApproveOnce => PendingInteractionResolutionDecision.ApproveOnce(),
            PendingInteractionResolutionActions.ApprovePattern => PendingInteractionResolutionDecision.ApprovePattern(),
            PendingInteractionResolutionActions.Deny => PendingInteractionResolutionDecision.Deny(),
            _ => PendingInteractionResolutionDecision.Submit()
        };
    }
}

/// <summary>
/// 挂起交互解决动作常量。
/// </summary>
public static class PendingInteractionResolutionActions
{
    /// <summary>
    /// 兼容旧审批动作。
    /// </summary>
    public const string Approve = "approve";

    /// <summary>
    /// 仅允许本次命令。
    /// </summary>
    public const string ApproveOnce = "approve-once";

    /// <summary>
    /// 允许当前会话中的同类命令。
    /// </summary>
    public const string ApprovePattern = "approve-pattern";

    /// <summary>
    /// 拒绝当前交互。
    /// </summary>
    public const string Deny = "deny";

    /// <summary>
    /// 提交补充信息。
    /// </summary>
    public const string Submit = "submit";
}

/// <summary>
/// 挂起交互解决裁决。
/// </summary>
public sealed record PendingInteractionResolutionDecision
{
    /// <summary>
    /// 归一后的动作。
    /// </summary>
    public string Action { get; init; } = PendingInteractionResolutionActions.Submit;

    /// <summary>
    /// CLI 审批授权范围。
    /// </summary>
    public CliApprovalGrantScope? ApprovalScope { get; init; }

    /// <summary>
    /// 是否拒绝交互。
    /// </summary>
    public bool IsDenied { get; init; }

    /// <summary>
    /// 用于恢复 Agent Loop 的用户可见消息。
    /// </summary>
    public string ResumeMessage { get; init; } = "我已补充所需信息，请继续。";

    /// <summary>
    /// 单次审批通过。
    /// </summary>
    public static PendingInteractionResolutionDecision ApproveOnce()
    {
        return new PendingInteractionResolutionDecision
        {
            Action = PendingInteractionResolutionActions.ApproveOnce,
            ApprovalScope = CliApprovalGrantScope.Once,
            ResumeMessage = "我已允许本次命令执行，请继续。"
        };
    }

    /// <summary>
    /// 同类命令审批通过。
    /// </summary>
    public static PendingInteractionResolutionDecision ApprovePattern()
    {
        return new PendingInteractionResolutionDecision
        {
            Action = PendingInteractionResolutionActions.ApprovePattern,
            ApprovalScope = CliApprovalGrantScope.Pattern,
            ResumeMessage = "我已允许当前会话中的同类命令继续执行，请继续。"
        };
    }

    /// <summary>
    /// 拒绝交互。
    /// </summary>
    public static PendingInteractionResolutionDecision Deny()
    {
        return new PendingInteractionResolutionDecision
        {
            Action = PendingInteractionResolutionActions.Deny,
            IsDenied = true,
            ResumeMessage = "我已拒绝当前操作，请停止本次执行。"
        };
    }

    /// <summary>
    /// 提交补充信息。
    /// </summary>
    public static PendingInteractionResolutionDecision Submit()
    {
        return new PendingInteractionResolutionDecision();
    }
}
