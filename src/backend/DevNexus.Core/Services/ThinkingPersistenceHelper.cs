using DevNexus.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevNexus.Core.Services.Chat;

namespace DevNexus.Core.Services;

/// <summary>
/// 思维链外部持久化辅助工具（用于直接 Hub 推送的 ThinkingBlock）
/// </summary>
public static class ThinkingPersistenceHelper
{
    /// <summary>
    /// 将外部通道产生的思维链追加到临时字段，供流完成时统一合并。
    /// </summary>
    /// <param name="dbContext">数据库上下文</param>
    /// <param name="messageId">消息 ID</param>
    /// <param name="thinkingContent">思维链内容</param>
    /// <param name="logger">日志记录器（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task PersistExternalThinkingAsync(
        IChatMessageRepository chatMessageRepository,
        Guid messageId,
        string thinkingContent,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (messageId == Guid.Empty || string.IsNullOrWhiteSpace(thinkingContent))
        {
            return;
        }

        try
        {
            var message = await chatMessageRepository.GetByIdAsync(messageId, cancellationToken);

            if (message == null)
            {
                logger?.LogWarning(
                    "[ThinkingPersistence] Message not found | MessageId={MessageId}",
                    messageId);
                return;
            }

            // 规范化内容（确保以换行符结尾）
            var normalized = thinkingContent.EndsWith("\n", StringComparison.Ordinal)
                ? thinkingContent
                : thinkingContent + "\n";

            // 初始化或追加到外部思维链临时字段
            if (!message.Content.ContainsKey("thinking_external_partial"))
            {
                message.Content["thinking_external_partial"] = string.Empty;
            }

            var existing = message.Content["thinking_external_partial"]?.ToString() ?? string.Empty;
            message.Content["thinking_external_partial"] = existing + normalized;
            var persisted = message.Content["thinking_external_partial"]?.ToString() ?? string.Empty;

            await chatMessageRepository.UpdateAsync(message, cancellationToken);

            logger?.LogDebug(
                "[Thinking.Trace] PersistPartial | Source={Source} MessageId={MessageId} DeltaLength={DeltaLength} " +
                "PreviousLength={PreviousLength} PersistedLength={PersistedLength} DeltaHash={DeltaHash} Preview={Preview}",
                "ExternalNotifier",
                messageId,
                normalized.Length,
                existing.Length,
                persisted.Length,
                ThinkingTraceHelper.ComputeHash(normalized),
                ThinkingTraceHelper.CreatePreview(normalized));
        }
        catch (Exception ex)
        {
            // 持久化失败不影响主流程（仅记录警告）
            logger?.LogWarning(
                ex,
                "[ThinkingPersistence] Failed to persist external thinking | MessageId={MessageId}",
                messageId);
        }
    }

    /// <summary>
    /// 读取并合并外部思维链到当前思维链内容。
    /// </summary>
    /// <param name="dbContext">数据库上下文</param>
    /// <param name="messageId">消息 ID</param>
    /// <param name="currentThinking">当前思维链内容</param>
    /// <param name="logger">日志记录器（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合并后的完整思维链</returns>
    public static async Task<string> MergeExternalThinkingAsync(
        IChatMessageRepository chatMessageRepository,
        Guid messageId,
        string currentThinking,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (messageId == Guid.Empty)
        {
            return currentThinking;
        }

        try
        {
            var message = await chatMessageRepository.GetByIdAsync(messageId, cancellationToken);

            if (message?.Content == null ||
                !message.Content.TryGetValue("thinking_external_partial", out var externalObj) ||
                externalObj == null)
            {
                return currentThinking;
            }

            var externalThinking = externalObj.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(externalThinking))
            {
                return currentThinking;
            }

            var mergedThinking = string.Concat(externalThinking, currentThinking);

            logger?.LogDebug(
                "[Thinking.Trace] FinalMergeExternal | MessageId={MessageId} ExternalLength={ExternalLength} " +
                "ExternalHash={ExternalHash} CurrentLength={CurrentLength} CurrentHash={CurrentHash} " +
                "MergedLength={MergedLength} MergedHash={MergedHash}",
                messageId,
                externalThinking.Length,
                ThinkingTraceHelper.ComputeHash(externalThinking),
                currentThinking.Length,
                ThinkingTraceHelper.ComputeHash(currentThinking),
                mergedThinking.Length,
                ThinkingTraceHelper.ComputeHash(mergedThinking));

            // 外部思维链在前（时间顺序：工具调用 → LLM 推理）
            return mergedThinking;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "[ThinkingPersistence] Failed to merge external thinking | MessageId={MessageId}",
                messageId);
            return currentThinking;
        }
    }
}
