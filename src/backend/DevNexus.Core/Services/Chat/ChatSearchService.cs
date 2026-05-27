// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天搜索服务 - 负责同步数据到 Elasticsearch
/// </summary>
public class ChatSearchService
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IElasticsearchSearchService? _searchService;
    private readonly ILogger<ChatSearchService> _logger;

    public ChatSearchService(
        IChatMessageRepository chatMessageRepository,
        ILogger<ChatSearchService> logger,
        IElasticsearchSearchService? searchService = null)
    {
        _chatMessageRepository = chatMessageRepository;
        _logger = logger;
        _searchService = searchService;
    }

    /// <summary>
    /// 同步消息到 Elasticsearch
    /// </summary>
    public async Task SyncMessageToElasticsearchAsync(
        ChatMessage message,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (_searchService == null) return;

        try
        {
            var messageDoc = new MessageSearchDocumentDto
            {
                Id = message.Id.ToString(),
                SessionId = message.ChatSessionId.ToString(),
                UserId = userId.ToString(),
                Role = message.SenderType,
                Content = message.Content.ContainsKey(ChatMessageContentKeys.Text)
                    ? message.Content[ChatMessageContentKeys.Text].ToString() ?? string.Empty
                    : string.Empty,
                CreatedAt = message.CreatedAt
            };

            await _searchService.IndexMessageAsync(messageDoc, cancellationToken);

            _logger.LogDebug("Synced message {MessageId} to Elasticsearch", message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync message {MessageId} to Elasticsearch", message.Id);
        }
    }

    /// <summary>
    /// 同步会话到 Elasticsearch
    /// </summary>
    public async Task SyncSessionToElasticsearchAsync(
        ChatSession session,
        CancellationToken cancellationToken = default)
    {
        if (_searchService == null) return;

        try
        {
            var lastMessage = await _chatMessageRepository.GetLatestBySessionAsync(session.Id, cancellationToken);

            var lastMessagePreview = lastMessage?.Content.ContainsKey(ChatMessageContentKeys.Text) == true
                ? lastMessage.Content[ChatMessageContentKeys.Text].ToString()
                : null;

            var sessionDoc = new SessionSearchDocumentDto
            {
                Id = session.Id.ToString(),
                UserId = session.UserId.ToString(),
                Title = session.Title,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
                LastMessagePreview = lastMessagePreview,
                MessageCount = await _chatMessageRepository.CountBySessionAsync(session.Id, cancellationToken)
            };

            await _searchService.IndexSessionAsync(sessionDoc, cancellationToken);

            _logger.LogDebug("Synced session {SessionId} to Elasticsearch", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync session {SessionId} to Elasticsearch", session.Id);
        }
    }

    /// <summary>
    /// 从 Elasticsearch 删除会话
    /// </summary>
    public async Task DeleteSessionFromElasticsearchAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (_searchService == null) return;

        try
        {
            await _searchService.DeleteSessionAsync(sessionId.ToString(), cancellationToken);
            await _searchService.DeleteSessionMessagesAsync(sessionId.ToString(), cancellationToken);

            _logger.LogDebug("Deleted session {SessionId} from Elasticsearch", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete session {SessionId} from Elasticsearch", sessionId);
        }
    }

    /// <summary>
    /// 从 Elasticsearch 删除单条消息
    /// </summary>
    public async Task DeleteMessageFromElasticsearchAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (_searchService == null) return;

        try
        {
            await _searchService.DeleteMessageAsync(messageId.ToString(), cancellationToken);
            _logger.LogDebug("Deleted message {MessageId} from Elasticsearch", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete message {MessageId} from Elasticsearch", messageId);
        }
    }

    /// <summary>
    /// 从 Elasticsearch 批量删除消息
    /// </summary>
    public async Task DeleteMessagesFromElasticsearchAsync(
        IEnumerable<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        if (_searchService == null) return;

        var ids = messageIds?.ToList();
        if (ids == null || ids.Count == 0) return;

        try
        {
            await _searchService.DeleteMessagesAsync(ids.Select(id => id.ToString()), cancellationToken);
            _logger.LogDebug("Deleted {Count} messages from Elasticsearch", ids.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to batch delete messages from Elasticsearch");
        }
    }
}
