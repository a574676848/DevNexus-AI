using DevNexus.Core.Abstractions.Observability;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Channels;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 后处理动作类型。
/// </summary>
public enum AgentLoopAction
{
    /// <summary>
    /// 不需要额外动作，继续正常收尾。
    /// </summary>
    None = 0,

    /// <summary>
    /// 停止自动修复，直接返回。
    /// </summary>
    Stop = 1,

    /// <summary>
    /// 需要创建修复消息并重试。
    /// </summary>
    Retry = 2
}

/// <summary>
/// Agent Loop 协调结果。
/// </summary>
public sealed class AgentLoopDecision
{
    /// <summary>
    /// 后处理动作。
    /// </summary>
    public AgentLoopAction Action { get; init; }

    /// <summary>
    /// 需要重试时生成的修复消息。
    /// </summary>
    public ChatMessage? RepairMessage { get; init; }
}

/// <summary>
/// 聊天 Agent Loop 协调器。
/// 负责评估是否停止自动修复、是否需要构造修复消息并发起下一轮。
/// </summary>
public sealed class ChatAgentLoopCoordinator
{
    private const string InternalRepairPromptMetadataKey = "internalRepairPrompt";

    private readonly AgentLoopExecutor _agentLoopExecutor;
    private readonly IAgentLoopRecoveryGuard _agentLoopRecoveryGuard;
    private readonly IAgentLoopMetricsCollector _metricsCollector;
    private readonly IDistributedTracingService _tracingService;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IPendingInteractionService _pendingInteractionService;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;
    private readonly ILogger<ChatAgentLoopCoordinator> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ChatAgentLoopCoordinator(
        AgentLoopExecutor agentLoopExecutor,
        IAgentLoopRecoveryGuard agentLoopRecoveryGuard,
        IAgentLoopMetricsCollector metricsCollector,
        IDistributedTracingService tracingService,
        IChatMessageRepository chatMessageRepository,
        IPendingInteractionService pendingInteractionService,
        IRuntimeEventNotifier runtimeEventNotifier,
        ILogger<ChatAgentLoopCoordinator> logger)
    {
        _agentLoopExecutor = agentLoopExecutor;
        _agentLoopRecoveryGuard = agentLoopRecoveryGuard;
        _metricsCollector = metricsCollector;
        _tracingService = tracingService;
        _chatMessageRepository = chatMessageRepository;
        _pendingInteractionService = pendingInteractionService;
        _runtimeEventNotifier = runtimeEventNotifier;
        _logger = logger;
    }

    /// <summary>
    /// 处理消息生成后的 Agent Loop 评估与修复决策。
    /// </summary>
    public async Task<AgentLoopDecision> HandleAsync(
        Guid sessionId,
        Guid userId,
        Guid providerId,
        string userQuery,
        string fullResponse,
        ChatMessage aiMessage,
        IReadOnlyList<ToolExecutionRecord> toolRecords,
        int agentLoopAttempt,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        var completionDecision = AgentLoopCompletionPolicy.Decide(toolRecords);
        if (completionDecision.IsComplete)
        {
            _logger.LogDebug(
                "[AgentLoop] 普通完成收尾 | Reason={Reason}",
                completionDecision.Reason);

            return new AgentLoopDecision { Action = AgentLoopAction.None };
        }

        var turnEventsUpdate = AgentTurnEventBuilder.BuildUpdatedDto(aiMessage.Id, toolRecords);
        await _tracingService.LogStructuredEventAsync(
            TraceEvent.AgentTurnEventsBuilt,
            "Debug",
            $"工具执行事件已归一化 | Count={turnEventsUpdate.Events.Count} Failed={turnEventsUpdate.Events.Count(item => item.Kind == AgentTurnEventKind.ToolFailed)}");

        await _runtimeEventNotifier.NotifyAsync(
            userId,
            sessionId,
            ServerEventType.AgentTurnEventsUpdated,
            turnEventsUpdate,
            cancellationToken);

        if (AgentLoopStopSignalPolicy.ShouldStop(fullResponse))
        {
            _logger.LogInformation(
                "[AgentLoop] LLM 主动停止重试 | Attempt={Attempt}",
                agentLoopAttempt + 1);

            await _tracingService.LogStructuredEventAsync(
                TraceEvent.AgentLoopRepairDecided,
                "Information",
                "LLM 判断问题无法通过重试解决，主动停止自动修复");

            await ThinkingContext.EmitAsync("🛑 AI 判断问题无法通过重试解决，已停止自动修复");

            await blockWriter.WriteAsync(new BlockDto
            {
                BlockId = Guid.NewGuid(),
                SessionId = sessionId,
                MessageId = aiMessage.Id,
                BlockType = BlockType.Warning,
                Content = "自动修复已停止：模型判断问题无法通过重试解决。你可以补充更多约束或手动调整后重新生成。",
                IsLast = false,
                Metadata = new Dictionary<string, object>
                {
                    { FeedbackBlockMetadataConstants.Level, FeedbackBlockMetadataConstants.LevelInfo },
                    { FeedbackBlockMetadataConstants.Title, "自动修复已停止" }
                }
            }, cancellationToken);

            return new AgentLoopDecision { Action = AgentLoopAction.Stop };
        }

        await _tracingService.LogStructuredEventAsync(
            TraceEvent.AgentLoopEvaluationStarted,
            "Debug",
            $"开始第 {agentLoopAttempt + 1} 次 Agent Loop 评估");

        var recoveryDecision = await _agentLoopRecoveryGuard.EvaluateAsync(
            userId,
            sessionId,
            toolRecords,
            agentLoopAttempt,
            cancellationToken);

        if (recoveryDecision.ShouldStop)
        {
            await _tracingService.LogStructuredEventAsync(
                TraceEvent.AgentLoopRepairDecided,
                "Information",
                $"Agent Loop 因运行态或不可恢复失败停止 | Title={recoveryDecision.StopTitle}");

            await WriteWarningBlockAsync(
                sessionId,
                aiMessage.Id,
                recoveryDecision.StopTitle ?? "自动修复已停止",
                recoveryDecision.StopMessage ?? "当前自动修复已停止，请先处理前置条件后再继续。",
                blockWriter,
                cancellationToken);

            return new AgentLoopDecision { Action = AgentLoopAction.Stop };
        }

        if (recoveryDecision.RequiresPendingInteraction
            && recoveryDecision.PendingInteractionTool != null)
        {
            var interaction = await _pendingInteractionService.CreateOrReuseAsync(
                sessionId,
                aiMessage.Id,
                recoveryDecision.PendingInteractionTool,
                evaluationFeedback: null,
                cancellationToken);

            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.PendingInteractionCreated,
                new
                {
                    InteractionId = interaction.Id,
                    Kind = interaction.Kind.ToWireValue(),
                    interaction.Title,
                    interaction.Description
                },
                cancellationToken);

            await _tracingService.LogStructuredEventAsync(
                TraceEvent.AgentLoopRepairDecided,
                "Information",
                $"Agent Loop 创建挂起交互并停止自动修复 | InteractionId={interaction.Id} Kind={interaction.Kind.ToWireValue()}");

            await WriteWarningBlockAsync(
                sessionId,
                aiMessage.Id,
                interaction.Title,
                interaction.Description,
                blockWriter,
                cancellationToken);

            return new AgentLoopDecision { Action = AgentLoopAction.Stop };
        }

        var evaluationStopwatch = Stopwatch.StartNew();
        var (needsRepair, repairPrompt) = await _agentLoopExecutor.EvaluateAndBuildRepairAsync(
            userQuery,
            fullResponse,
            recoveryDecision.ToolRecords.ToList(),
            agentLoopAttempt,
            providerId,
            cancellationToken);
        evaluationStopwatch.Stop();

        await _tracingService.LogStructuredEventAsync(
            TraceEvent.AgentLoopEvaluationCompleted,
            "Debug",
            $"Agent Loop 评估完成，需要修复: {needsRepair}",
            null);

        await _metricsCollector.RecordRepairAttempt(
            needsRepair,
            evaluationStopwatch.ElapsedMilliseconds);

        if (!needsRepair || string.IsNullOrEmpty(repairPrompt))
        {
            await _tracingService.LogStructuredEventAsync(
                TraceEvent.AgentLoopEvaluationCompleted,
                "Information",
                "Agent Loop 评估通过，无需修复");

            return new AgentLoopDecision { Action = AgentLoopAction.None };
        }

        await _tracingService.LogStructuredEventAsync(
            TraceEvent.AgentLoopRepairAttemptStarted,
            "Information",
            $"启动第 {agentLoopAttempt + 1} 次自动修复尝试");

        await ThinkingContext.EmitAsync($"🔄 第 {agentLoopAttempt + 1} 次自动修复尝试...");

        var repairMessage = new ChatMessage
        {
            ChatSessionId = sessionId,
            ParentMessageId = aiMessage.Id,
            SenderId = userId,
            SenderType = ChatConstants.RoleUser,
            Content = new Dictionary<string, object> { { ChatMessageContentKeys.Text, repairPrompt } },
            MessageType = ChatConstants.MessageTypeText,
            Metadata = new Dictionary<string, object>
            {
                [InternalRepairPromptMetadataKey] = true
            },
            Status = ChatConstants.StatusCompleted
        };

        await _chatMessageRepository.AddAsync(repairMessage, cancellationToken);

        return new AgentLoopDecision
        {
            Action = AgentLoopAction.Retry,
            RepairMessage = repairMessage
        };
    }

    private static Task WriteWarningBlockAsync(
        Guid sessionId,
        Guid messageId,
        string title,
        string description,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        return blockWriter.WriteAsync(new BlockDto
        {
            BlockId = Guid.NewGuid(),
            SessionId = sessionId,
            MessageId = messageId,
            BlockType = BlockType.Warning,
            Content = $"{title}：{description}",
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { FeedbackBlockMetadataConstants.Level, FeedbackBlockMetadataConstants.LevelInfo },
                { FeedbackBlockMetadataConstants.Title, title }
            }
        }, cancellationToken).AsTask();
    }
}
