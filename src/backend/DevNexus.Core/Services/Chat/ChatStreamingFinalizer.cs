using Microsoft.Extensions.Logging;
using System.Text;
using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天流式生成收尾服务。
/// 负责完成、取消、异常三种收尾场景下的消息内容整理与持久化。
/// </summary>
public sealed class ChatStreamingFinalizer
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ChatThinkingPersistenceCoordinator _thinkingCoordinator;
    private readonly ILogger<ChatStreamingFinalizer> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ChatStreamingFinalizer(
        IChatMessageRepository chatMessageRepository,
        ChatThinkingPersistenceCoordinator thinkingCoordinator,
        ILogger<ChatStreamingFinalizer> logger)
    {
        _chatMessageRepository = chatMessageRepository;
        _thinkingCoordinator = thinkingCoordinator;
        _logger = logger;
    }

    /// <summary>
    /// 完成流式生成后的最终消息整理与持久化。
    /// </summary>
    public async Task FinalizeCompletedAsync(
        ChatMessage aiMessage,
        Guid sessionId,
        string finalText,
        bool isTruncated,
        StringBuilder? preParserThinking,
        string parserThinking,
        string contextThinking,
        CancellationToken cancellationToken = default)
    {
        ChatThinkingPersistenceCoordinator.LogThinkingFinalAssemble(
            _logger,
            sessionId,
            aiMessage.Id,
            preParserThinking?.ToString() ?? string.Empty,
            parserThinking,
            contextThinking);

        var thinkingContent = ChatThinkingPersistenceCoordinator.MergeThinkingForPersistence(preParserThinking, parserThinking);
        if (!string.IsNullOrEmpty(contextThinking))
        {
            thinkingContent += contextThinking;
        }

        thinkingContent = await _thinkingCoordinator.MergeExternalThinkingAsync(aiMessage.Id, thinkingContent, cancellationToken);
        var previousThinkingPartial = GetTransientContent(aiMessage, "thinking_partial");

        aiMessage.Content = new Dictionary<string, object>
        {
            { "text", finalText }
        };

        if (!string.IsNullOrEmpty(previousThinkingPartial))
        {
            aiMessage.Content["thinking_partial"] = previousThinkingPartial;
        }

        ApplyThinkingContent(aiMessage, thinkingContent);

        aiMessage.Status = isTruncated ? ChatConstants.StatusTruncated : ChatConstants.StatusCompleted;
        aiMessage.UpdatedAt = DateTime.UtcNow;

        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);
    }

    /// <summary>
    /// 取消流式生成后的最终消息整理与持久化。
    /// </summary>
    public async Task FinalizeCancelledAsync(
        ChatMessage aiMessage,
        Guid sessionId,
        string? partialText,
        StringBuilder? preParserThinking,
        string? parserThinking,
        string contextThinking,
        bool streamStarted,
        CancellationToken cancellationToken = default)
    {
        var previousThinkingPartial = GetTransientContent(aiMessage, "thinking_partial");
        aiMessage.Content = new Dictionary<string, object>
        {
            { "text", partialText ?? string.Empty }
        };

        if (!string.IsNullOrEmpty(previousThinkingPartial))
        {
            aiMessage.Content["thinking_partial"] = previousThinkingPartial;
        }

        var resolvedParserThinking = streamStarted ? parserThinking ?? string.Empty : string.Empty;
        ChatThinkingPersistenceCoordinator.LogThinkingFinalAssemble(
            _logger,
            sessionId,
            aiMessage.Id,
            preParserThinking?.ToString() ?? string.Empty,
            resolvedParserThinking,
            contextThinking);

        var thinkingContent = ChatThinkingPersistenceCoordinator.MergeThinkingForPersistence(preParserThinking, resolvedParserThinking);
        if (!string.IsNullOrEmpty(contextThinking))
        {
            thinkingContent += contextThinking;
        }

        thinkingContent = await _thinkingCoordinator.MergeExternalThinkingAsync(aiMessage.Id, thinkingContent, cancellationToken);
        ApplyThinkingContent(aiMessage, thinkingContent);

        aiMessage.Status = ChatConstants.StatusCancelled;
        aiMessage.UpdatedAt = DateTime.UtcNow;

        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);
    }

    /// <summary>
    /// 异常结束时整理错误消息并持久化。
    /// </summary>
    public async Task<string> FinalizeErroredAsync(
        ChatMessage aiMessage,
        string? partialText,
        string errorDetails,
        CancellationToken cancellationToken = default)
    {
        var errorMessage = $"生成响应时发生错误：{errorDetails}";
        var renderedError = string.IsNullOrEmpty(partialText)
            ? $"> ❌ **{errorMessage}**\n"
            : $"\n\n> ❌ **{errorMessage}**\n";

        aiMessage.Status = ChatConstants.StatusError;
        aiMessage.Content = new Dictionary<string, object>
        {
            { "text", (partialText ?? string.Empty) + renderedError }
        };
        aiMessage.UpdatedAt = DateTime.UtcNow;

        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);
        return renderedError;
    }

    private void ApplyThinkingContent(ChatMessage aiMessage, string thinkingContent)
    {
        _thinkingCoordinator.ApplyFinalThinking(aiMessage, thinkingContent);
    }

    private static string? GetTransientContent(ChatMessage aiMessage, string key)
    {
        if (!aiMessage.Content.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        var text = value.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

}
