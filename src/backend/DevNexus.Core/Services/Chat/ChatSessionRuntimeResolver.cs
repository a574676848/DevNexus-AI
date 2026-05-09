using DevNexus.Core.Models.Cli;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 会话统一运行态解析结果。
/// </summary>
public sealed record ChatSessionRuntimeSnapshot
{
    /// <summary>
    /// 统一运行态。
    /// </summary>
    public ChatSessionRunState RunState { get; init; } = ChatSessionRunState.Idle;

    /// <summary>
    /// 发送决策。
    /// </summary>
    public ChatExecutionDecision ExecutionDecision { get; init; } = ChatExecutionDecision.Immediate;

    /// <summary>
    /// 活跃挂起交互数量。
    /// </summary>
    public int PendingInteractionCount { get; init; }

    /// <summary>
    /// 主挂起交互类型。
    /// </summary>
    public PendingInteractionKind PrimaryPendingInteractionKind { get; init; } = PendingInteractionKind.Unknown;

    /// <summary>
    /// 主挂起交互标识。
    /// </summary>
    public Guid? PrimaryPendingInteractionId { get; init; }

    /// <summary>
    /// 主挂起交互标题。
    /// </summary>
    public string? PrimaryPendingInteractionTitle { get; init; }

    /// <summary>
    /// 主挂起交互说明。
    /// </summary>
    public string? PrimaryPendingInteractionDescription { get; init; }

    /// <summary>
    /// 排队数量。
    /// </summary>
    public int QueuedCount { get; init; }

    /// <summary>
    /// 是否存在活跃 CLI 会话。
    /// </summary>
    public bool HasActiveCliSession { get; init; }

    /// <summary>
    /// CLI 是否等待输入。
    /// </summary>
    public bool CliWaitingForInput { get; init; }

    /// <summary>
    /// 是否存在进行中的助手消息。
    /// </summary>
    public bool HasInProgressAssistantMessage { get; init; }
}

/// <summary>
/// 会话统一运行态解析器。
/// 负责从挂起交互、CLI、排队和消息状态推导唯一运行态与发送决策。
/// </summary>
internal static class ChatSessionRuntimeResolver
{
    /// <summary>
    /// 解析统一运行态快照。
    /// </summary>
    public static ChatSessionRuntimeSnapshot Resolve(
        IReadOnlyList<PendingInteraction> pendingInteractions,
        CliSessionRuntimeSnapshot? cliSnapshot,
        int queuedCount,
        ChatMessage? latestAssistantMessage)
    {
        var primaryPendingInteraction = ResolvePrimaryPendingInteraction(pendingInteractions);
        var primaryPendingInteractionKind = primaryPendingInteraction?.Kind ?? PendingInteractionKind.Unknown;
        var hasActiveCliSession = cliSnapshot?.State is CliSessionExecutionState.Created
            or CliSessionExecutionState.Running
            or CliSessionExecutionState.WaitingForInput;
        var cliWaitingForInput = cliSnapshot?.WaitingForInput == true;
        var hasInProgressAssistantMessage = ChatConstants.IsInProgressStatus(latestAssistantMessage?.Status);

        if (primaryPendingInteractionKind == PendingInteractionKind.Approval)
        {
            return Create(
                ChatSessionRunState.WaitingForApproval,
                ChatExecutionDecision.Rejected,
                pendingInteractions.Count,
                primaryPendingInteractionKind,
                primaryPendingInteraction?.Id,
                primaryPendingInteraction?.Title,
                primaryPendingInteraction?.Description,
                queuedCount,
                hasActiveCliSession,
                cliWaitingForInput,
                hasInProgressAssistantMessage);
        }

        if (pendingInteractions.Count > 0)
        {
            return Create(
                ChatSessionRunState.WaitingForPendingInput,
                ChatExecutionDecision.Rejected,
                pendingInteractions.Count,
                primaryPendingInteractionKind,
                primaryPendingInteraction?.Id,
                primaryPendingInteraction?.Title,
                primaryPendingInteraction?.Description,
                queuedCount,
                hasActiveCliSession,
                cliWaitingForInput,
                hasInProgressAssistantMessage);
        }

        if (cliSnapshot?.WaitingForInput == true)
        {
            return Create(
                ChatSessionRunState.WaitingForInput,
                ChatExecutionDecision.ForwardToRuntimeInput,
                pendingInteractions.Count,
                primaryPendingInteractionKind,
                primaryPendingInteraction?.Id,
                primaryPendingInteraction?.Title,
                primaryPendingInteraction?.Description,
                queuedCount,
                hasActiveCliSession,
                cliWaitingForInput,
                hasInProgressAssistantMessage);
        }

        if (cliSnapshot?.State is CliSessionExecutionState.Created or CliSessionExecutionState.Running)
        {
            return Create(
                ChatSessionRunState.Running,
                ChatExecutionDecision.Queued,
                pendingInteractions.Count,
                primaryPendingInteractionKind,
                primaryPendingInteraction?.Id,
                primaryPendingInteraction?.Title,
                primaryPendingInteraction?.Description,
                queuedCount,
                hasActiveCliSession,
                cliWaitingForInput,
                hasInProgressAssistantMessage);
        }

        if (queuedCount > 0)
        {
            return Create(
                ChatSessionRunState.Queued,
                ChatExecutionDecision.Queued,
                pendingInteractions.Count,
                primaryPendingInteractionKind,
                primaryPendingInteraction?.Id,
                primaryPendingInteraction?.Title,
                primaryPendingInteraction?.Description,
                queuedCount,
                hasActiveCliSession,
                cliWaitingForInput,
                hasInProgressAssistantMessage);
        }

        if (ChatConstants.IsInProgressStatus(latestAssistantMessage?.Status))
        {
            return Create(
                ChatSessionRunState.Generating,
                ChatExecutionDecision.Immediate,
                pendingInteractions.Count,
                primaryPendingInteractionKind,
                primaryPendingInteraction?.Id,
                primaryPendingInteraction?.Title,
                primaryPendingInteraction?.Description,
                queuedCount,
                hasActiveCliSession,
                cliWaitingForInput,
                hasInProgressAssistantMessage);
        }

        return Create(
            ChatSessionRunState.Idle,
            ChatExecutionDecision.Immediate,
            pendingInteractions.Count,
            primaryPendingInteractionKind,
            primaryPendingInteraction?.Id,
            primaryPendingInteraction?.Title,
            primaryPendingInteraction?.Description,
            queuedCount,
            hasActiveCliSession,
            cliWaitingForInput,
            hasInProgressAssistantMessage);
    }

    private static ChatSessionRuntimeSnapshot Create(
        ChatSessionRunState runState,
        ChatExecutionDecision executionDecision,
        int pendingInteractionCount,
        PendingInteractionKind primaryPendingInteractionKind,
        Guid? primaryPendingInteractionId,
        string? primaryPendingInteractionTitle,
        string? primaryPendingInteractionDescription,
        int queuedCount,
        bool hasActiveCliSession,
        bool cliWaitingForInput,
        bool hasInProgressAssistantMessage)
    {
        return new ChatSessionRuntimeSnapshot
        {
            RunState = runState,
            ExecutionDecision = executionDecision,
            PendingInteractionCount = pendingInteractionCount,
            PrimaryPendingInteractionKind = primaryPendingInteractionKind,
            PrimaryPendingInteractionId = primaryPendingInteractionId,
            PrimaryPendingInteractionTitle = primaryPendingInteractionTitle,
            PrimaryPendingInteractionDescription = primaryPendingInteractionDescription,
            QueuedCount = queuedCount,
            HasActiveCliSession = hasActiveCliSession,
            CliWaitingForInput = cliWaitingForInput,
            HasInProgressAssistantMessage = hasInProgressAssistantMessage
        };
    }

    private static PendingInteraction? ResolvePrimaryPendingInteraction(
        IReadOnlyList<PendingInteraction> pendingInteractions)
    {
        if (pendingInteractions.Count == 0)
        {
            return null;
        }

        var approvalInteraction = pendingInteractions
            .OrderByDescending(interaction => interaction.CreatedAt)
            .FirstOrDefault(interaction => interaction.Kind == PendingInteractionKind.Approval);
        if (approvalInteraction != null)
        {
            return approvalInteraction;
        }

        return pendingInteractions
            .OrderByDescending(interaction => interaction.CreatedAt)
            .FirstOrDefault();
    }
}
