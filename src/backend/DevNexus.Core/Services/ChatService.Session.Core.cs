// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using DevNexus.Core.Extensions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务 - 会话管理核心
/// </summary>
public partial class ChatService
{
    /// <inheritdoc />
    public async Task<Guid> CreateChatSessionAsync(
        Guid userId,
        string title,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Creating chat session for user {UserId} with title {Title}",
            userId,
            title);

        var chatSession = new ChatSession
        {
            UserId = userId,
            Title = title,
            IsActive = true
        };

        await _chatSessionRepository.AddAsync(chatSession, cancellationToken);

        // 同步到 Elasticsearch
        await _chatSearchService.SyncSessionToElasticsearchAsync(chatSession, cancellationToken);

        return chatSession.Id;
    }

    /// <inheritdoc />
    public async Task<List<ChatSessionDto>> GetChatSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting chat sessions for user {UserId}",
            userId);

        var sessions = await _chatSessionRepository.ListByUserAsync(userId, cancellationToken);

        // 转换为 DTO
        return sessions.Select(session =>
        {
            var lastMessage = session.Messages.FirstOrDefault();
            return BuildChatSessionDto(session, session.Messages.Count, lastMessage);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<ChatSessionDto?> GetChatSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _chatSessionRepository.GetByIdAsync(userId, sessionId, cancellationToken);

        if (session == null) return null;

        var messageCount = await _chatMessageRepository.CountBySessionAsync(sessionId, cancellationToken);
        var lastMessage = await _chatMessageRepository.GetLatestBySessionAsync(sessionId, cancellationToken);

        return BuildChatSessionDto(session, messageCount, lastMessage);
    }

    /// <inheritdoc />
    public async Task DeleteChatSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Deleting chat session {SessionId} for user {UserId}",
            sessionId,
            userId);

        // 查找会话并验证所有权
        var session = await _chatSessionRepository.GetByIdAsync(userId, sessionId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning(
                "Chat session {SessionId} not found or not owned by user {UserId}",
                sessionId,
                userId);
            throw new InvalidOperationException("会话不存在或无权删除");
        }

        await _chatSessionDeletionCoordinator.DeleteAsync(session, userId, cancellationToken);
    }
}
