// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using DevNexus.Core.Extensions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务 - 会话上下文更新
/// </summary>
public partial class ChatService
{
    /// <inheritdoc />
    public async Task<ChatSessionDto> UpdateChatSessionAsync(
        Guid sessionId,
        Guid userId,
        ChatSessionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Updating chat session {SessionId} for user {UserId}",
            sessionId,
            userId);

        var session = await GetOwnedChatSessionEntityAsync(sessionId, userId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            session.Title = request.Title.Trim();
        }

        session.UpdatedAt = DateTime.UtcNow;
        await _chatSessionRepository.UpdateAsync(session, cancellationToken);

        // 同步到 Elasticsearch
        await _chatSearchService.SyncSessionToElasticsearchAsync(session, cancellationToken);

        var messageCount = await _chatMessageRepository.CountBySessionAsync(sessionId, cancellationToken);
        var lastMessage = await _chatMessageRepository.GetLatestBySessionAsync(sessionId, cancellationToken);

        _logger.LogInformation(
            "Chat session {SessionId} updated.",
            sessionId);

        return BuildChatSessionDto(session, messageCount, lastMessage);
    }

    private async Task<ChatSession> GetOwnedChatSessionEntityAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var session = await _chatSessionRepository.GetByIdAsync(userId, sessionId, cancellationToken);

        if (session != null)
        {
            return session;
        }

        _logger.LogWarning(
            "Chat session {SessionId} not found or not owned by user {UserId}",
            sessionId,
            userId);

        throw new InvalidOperationException("会话不存在或无权更新");
    }
}
