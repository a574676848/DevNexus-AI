// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using DevNexus.Core.Extensions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务 - DTO 构建和智能标题生成
/// </summary>
public partial class ChatService
{
    private ChatSessionDto BuildChatSessionDto(ChatSession session, int messageCount, ChatMessage? lastMessage)
    {
        return new ChatSessionDto
        {
            Id = session.Id,
            Title = session.Title,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            IsActive = session.IsActive,
            MessageCount = messageCount,
            LLMProviderId = session.LLMProviderId,
            LLMProviderName = session.LLMProvider?.DisplayName,
            LastMessage = lastMessage != null ? new ChatMessageDto
            {
                Id = lastMessage.Id,
                ChatSessionId = lastMessage.ChatSessionId,
                SenderType = lastMessage.SenderType,
                Content = lastMessage.Content.ContainsKey(ChatMessageContentKeys.Text)
                    ? lastMessage.Content[ChatMessageContentKeys.Text].ToString() ?? string.Empty
                    : string.Empty,
                CreatedAt = lastMessage.CreatedAt
            } : null,
        };
    }

    /// <summary>
    /// 获取或创建聊天会话
    /// </summary>
    private async Task<ChatSession> GetOrCreateChatSessionAsync(
        ChatRequest chatRequest,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (chatRequest.SessionId.HasValue)
        {
            // 检查会话是否存在且属于该用户
            var existingSession = await _chatSessionRepository.GetByIdAsync(
                userId,
                chatRequest.SessionId.Value,
                cancellationToken);

            if (existingSession != null)
            {
                // 更新会话的 LLM Provider ID（如果请求中指定了）
                if (chatRequest.LLMProviderId.HasValue && existingSession.LLMProviderId != chatRequest.LLMProviderId)
                {
                    existingSession.LLMProviderId = chatRequest.LLMProviderId;
                    existingSession.UpdatedAt = DateTime.UtcNow;
                    await _chatSessionRepository.UpdateAsync(existingSession, cancellationToken);

                    _logger.LogInformation(
                        "[AI.Chat] Updated session LLM Provider | SessionId={SessionId} LLMProviderId={LLMProviderId}",
                        existingSession.Id,
                        chatRequest.LLMProviderId);
                }

                return existingSession;
            }
        }

        // 创建新会话
        return await CreateNewChatSessionAsync(chatRequest, userId, cancellationToken);
    }

    /// <summary>
    /// 创建新的聊天会话
    /// </summary>
    private async Task<ChatSession> CreateNewChatSessionAsync(
        ChatRequest chatRequest,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 使用消息前 50 个字符作为会话标题
        string messageContent = chatRequest.Content;
        var sessionTitle = messageContent.Length > 50
            ? messageContent[..50] + "..."
            : messageContent;

        var chatSession = new ChatSession
        {
            LLMProviderId = chatRequest.LLMProviderId,
            UserId = userId,
            Title = sessionTitle,
            IsActive = true
        };

        await _chatSessionRepository.AddAsync(chatSession, cancellationToken);

        // 同步到 Elasticsearch
        await _chatSearchService.SyncSessionToElasticsearchAsync(chatSession, cancellationToken);

        return chatSession;
    }

    /// <inheritdoc />
    public async Task<string> GenerateSmartTitleAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[AI.SmartTitle] Generating smart title for session {SessionId}",
            sessionId);

        // 1. 验证会话所有权
        var session = await _chatSessionRepository.GetByIdAsync(userId, sessionId, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException("会话不存在或无权访问");
        }

        // 2. 获取最近的对话消息（最多 6 条）
        var recentMessages = (await _chatMessageRepository.ListRecentBySessionAsync(
            sessionId,
            6,
            cancellationToken))
            .OrderBy(message => message.CreatedAt)
            .ToList();

        if (recentMessages.Count < 2)
        {
            // 消息太少，使用默认标题
            return session.Title;
        }

        // 3. 构建用于生成标题的对话摘要
        var conversationSummary = string.Join("\n", recentMessages.Select(m =>
        {
            var role = ChatConstants.IsUserSender(m.SenderType) ? "用户" : "AI";
            var content = m.Content.ContainsKey(ChatMessageContentKeys.Text)
                ? m.Content[ChatMessageContentKeys.Text].ToString() ?? ""
                : "";
            // 截取每条消息的前 100 个字符
            var truncated = content.Length > 100 ? content[..100] + "..." : content;
            return $"{role}: {truncated}";
        }));

        // 4. 构建 Prompt
        var prompt = string.Format(PromptConstants.Chat.GenerateSmartTitle, conversationSummary);


        try
        {
            // 5. 调用 LLM 生成标题
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            // 获取默认 Provider
            var defaultProvider = await _llmProviderService.GetDefaultProviderAsync(cancellationToken);
            if (defaultProvider == null)
            {
                _logger.LogWarning("[AI.SmartTitle] No default LLM provider configured");
                return session.Title;
            }

            // 补全上下文 ID (using _chatHistoryService)
            var (finalUserId, latestMessageId) = await _chatHistoryService.EnrichSessionParamsAsync(sessionId, userId, cancellationToken);

            var result = await _IKernelService.GetChatCompletionAsync(
                chatHistory,
                defaultProvider.Id,
                sessionId: sessionId,
                messageId: latestMessageId,
                userId: finalUserId,
                cancellationToken: cancellationToken);

            var generatedTitle = result.Content?.Trim() ?? "";

            // 6. 清理标题（移除引号、换行等）
            generatedTitle = generatedTitle
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            // 限制标题长度
            if (generatedTitle.Length > 30)
            {
                generatedTitle = generatedTitle[..27] + "...";
            }

            if (string.IsNullOrEmpty(generatedTitle))
            {
                return session.Title;
            }

            // 7. 更新会话标题
            session.Title = generatedTitle;
            session.UpdatedAt = DateTime.UtcNow;
            await _chatSessionRepository.UpdateAsync(session, cancellationToken);

            // 同步到 Elasticsearch
            await _chatSearchService.SyncSessionToElasticsearchAsync(session, cancellationToken);

            _logger.LogInformation(
                "[AI.SmartTitle] Generated title for session {SessionId}: {Title}",
                sessionId,
                generatedTitle);

            return generatedTitle;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[AI.SmartTitle] Failed to generate smart title for session {SessionId}",
                sessionId);

            // 生成失败时返回原标题
            return session.Title;
        }
    }
}
