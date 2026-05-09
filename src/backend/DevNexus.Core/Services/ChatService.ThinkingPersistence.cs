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
    private async Task TriggerMemoryConsolidationCheckAsync(
        ChatSession chatSession,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            // 获取当前消息数
            var currentMessageCount = await _chatMessageRepository.CountBySessionAsync(chatSession.Id, cancellationToken);

            var lastConsolidatedCount = chatSession.LastConsolidatedMessageCount;
            var messagesSinceLastConsolidation = currentMessageCount - lastConsolidatedCount;

            _logger.LogDebug(
                "[MemoryConsolidation.Trigger] Checking session | SessionId={SessionId} CurrentCount={Current} LastCount={Last} Delta={Delta}",
                chatSession.Id,
                currentMessageCount,
                lastConsolidatedCount,
                messagesSinceLastConsolidation);

            // 策略2: 消息阈值检查 - 每增加10条消息立即触发
            if (messagesSinceLastConsolidation >= MemoryConsolidationMessageThreshold)
            {
                _logger.LogInformation(
                    "[MemoryConsolidation.Trigger] Threshold reached, enqueueing immediately | SessionId={SessionId} Delta={Delta}",
                    chatSession.Id,
                    messagesSinceLastConsolidation);

                // 取消现有的延迟任务
                if (!string.IsNullOrEmpty(chatSession.MemoryConsolidationJobId))
                {
                    _backgroundJobService.CancelMemoryConsolidation(chatSession.MemoryConsolidationJobId);
                }

                // 立即入队
                var jobId = _backgroundJobService.EnqueueMemoryConsolidation(chatSession.Id, userId);

                // 更新会话状态
                chatSession.MemoryConsolidationJobId = jobId;
                await _chatSessionRepository.UpdateAsync(chatSession, cancellationToken);

                return;
            }

            // 策略1: 30分钟不活跃延迟触发
            // 取消现有的延迟任务（如果有）
            if (!string.IsNullOrEmpty(chatSession.MemoryConsolidationJobId))
            {
                _backgroundJobService.CancelMemoryConsolidation(chatSession.MemoryConsolidationJobId);
            }

            // 只有有足够消息时才调度延迟任务
            if (currentMessageCount >= 3)
            {
                var newJobId = _backgroundJobService.ScheduleMemoryConsolidation(
                    chatSession.Id,
                    userId,
                    MemoryConsolidationDelay);

                chatSession.MemoryConsolidationJobId = newJobId;
                await _chatSessionRepository.UpdateAsync(chatSession, cancellationToken);

                _logger.LogDebug(
                    "[MemoryConsolidation.Trigger] Scheduled delayed consolidation | SessionId={SessionId} JobId={JobId} Delay={Delay}",
                    chatSession.Id,
                    newJobId,
                    MemoryConsolidationDelay);
            }
        }
        catch (Exception ex)
        {
            // 记忆沉淀触发失败不应影响正常聊天流程
            _logger.LogWarning(
                ex,
                "[MemoryConsolidation.Trigger] Failed to trigger consolidation check | SessionId={SessionId}",
                chatSession.Id);
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
