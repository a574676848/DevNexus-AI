using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using DevNexus.Core.Services.Chat;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务 - 思维链持久化与记忆沉淀
/// </summary>
public partial class ChatService
{
    // ======================== 记忆沉淀触发 ========================

    /// <summary>
    /// 触发记忆沉淀检查。
    /// 策略1：重置30分钟不活跃延迟任务；策略2：检查消息阈值，达到10条增量时立即触发。
    /// </summary>
    private async Task<MemoryConsolidationTriggerDecision?> TriggerMemoryConsolidationCheckAsync(
        ChatSession chatSession,
        Guid userId,
        ChatHistoryGovernanceSnapshot? historyGovernance,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentMessageCount = await _chatMessageRepository.CountBySessionAsync(chatSession.Id, cancellationToken);
            var lastConsolidatedCount = chatSession.LastConsolidatedMessageCount;
            var decision = MemoryConsolidationTriggerPolicy.Decide(
                currentMessageCount,
                lastConsolidatedCount,
                MemoryConsolidationMessageThreshold,
                minimumDelayedMessageCount: 3,
                historyGovernance,
                !string.IsNullOrEmpty(chatSession.MemoryConsolidationJobId));

            _logger.LogDebug(
                "[MemoryConsolidation.Trigger] Checking session | SessionId={SessionId} CurrentCount={Current} " +
                "LastCount={Last} Delta={Delta} Reason={Reason} ContextStrategy={ContextStrategy} ContextPressureReason={ContextPressureReason}",
                chatSession.Id,
                currentMessageCount,
                lastConsolidatedCount,
                decision.MessageDelta,
                decision.Reason,
                historyGovernance?.Strategy ?? ChatHistoryGovernanceStrategies.Empty,
                decision.ContextPressureReason);

            if (decision.ShouldEnqueueImmediately)
            {
                _logger.LogInformation(
                    "[MemoryConsolidation.Trigger] Enqueueing immediately | SessionId={SessionId} Delta={Delta} Reason={Reason} ContextPressureReason={ContextPressureReason}",
                    chatSession.Id,
                    decision.MessageDelta,
                    decision.Reason,
                    decision.ContextPressureReason);

                if (decision.ShouldCancelExistingJob && !string.IsNullOrEmpty(chatSession.MemoryConsolidationJobId))
                {
                    _backgroundJobService.CancelMemoryConsolidation(chatSession.MemoryConsolidationJobId);
                }

                var jobId = _backgroundJobService.EnqueueMemoryConsolidation(chatSession.Id, userId);
                chatSession.MemoryConsolidationJobId = jobId;
                await _chatSessionRepository.UpdateAsync(chatSession, cancellationToken);

                return decision;
            }

            if (!decision.ShouldScheduleDelayed)
            {
                return decision;
            }

            if (decision.ShouldCancelExistingJob && !string.IsNullOrEmpty(chatSession.MemoryConsolidationJobId))
            {
                _backgroundJobService.CancelMemoryConsolidation(chatSession.MemoryConsolidationJobId);
            }

            var newJobId = _backgroundJobService.ScheduleMemoryConsolidation(
                chatSession.Id,
                userId,
                MemoryConsolidationDelay);

            chatSession.MemoryConsolidationJobId = newJobId;
            await _chatSessionRepository.UpdateAsync(chatSession, cancellationToken);

            _logger.LogDebug(
                "[MemoryConsolidation.Trigger] Scheduled delayed consolidation | SessionId={SessionId} JobId={JobId} Delay={Delay} Reason={Reason}",
                chatSession.Id,
                newJobId,
                MemoryConsolidationDelay,
                decision.Reason);

            return decision;
        }
        catch (Exception ex)
        {
            // 记忆沉淀触发失败不应影响正常聊天流程
            _logger.LogWarning(
                ex,
                "[MemoryConsolidation.Trigger] Failed to trigger consolidation check | SessionId={SessionId}",
                chatSession.Id);
            return null;
        }
    }

    // ======================== 周期性思维链持久化（Swarm 长任务保护） ========================

    /// <summary>
    /// 周期性持久化部分思维链内容（不阻塞流式输出）。
    /// 用于保护长时间运行的 Swarm 任务在应用崩溃时的数据丢失。
    /// </summary>
    private async Task PersistPartialThinkingAsync(
        Guid sessionId,
        Guid messageId,
        string partialThinking)
    {
        await _thinkingPersistenceCoordinator.PersistPartialThinkingAsync(sessionId, messageId, partialThinking);
    }

    /// <summary>
    /// 周期性持久化部分文本内容（普通流式任务保护）
    /// </summary>
    private async Task PersistPartialTextAsync(
        Guid sessionId,
        Guid messageId,
        string partialText)
    {
        await _thinkingPersistenceCoordinator.PersistPartialTextAsync(sessionId, messageId, partialText);
    }

    /// <summary>
    /// 合并外部通道写入的思维链（如工具调用通知），避免被最终覆盖丢失。
    /// </summary>
    private async Task<string> MergeExternalThinkingForPersistenceAsync(Guid messageId, string currentThinking)
    {
        return await _thinkingPersistenceCoordinator.MergeExternalThinkingAsync(messageId, currentThinking, CancellationToken.None);
    }

    private void LogThinkingFinalAssemble(
        Guid sessionId,
        Guid messageId,
        string preThinking,
        string parserThinking,
        string contextThinking)
    {
        ChatThinkingPersistenceCoordinator.LogThinkingFinalAssemble(
            _logger,
            sessionId,
            messageId,
            preThinking,
            parserThinking,
            contextThinking);
    }
}
