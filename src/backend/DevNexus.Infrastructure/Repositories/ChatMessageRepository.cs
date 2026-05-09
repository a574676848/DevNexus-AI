using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 聊天消息仓储实现。
/// </summary>
public class ChatMessageRepository : IChatMessageRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ChatMessageRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ChatMessages
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChatMessages
            .Where(message => message.ChatSessionId == sessionId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> ListRecentBySessionAsync(
        Guid sessionId,
        int takeCount,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.ChatSessionId == sessionId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(takeCount)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListIdsBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChatMessages
            .Where(message => message.ChatSessionId == sessionId)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<ChatMessage?> GetByIdWithSessionAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ChatMessages
            .Include(message => message.ChatSession)
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> ListBySessionAndIdsAsync(
        Guid sessionId,
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChatMessages
            .Where(message => message.ChatSessionId == sessionId && messageIds.Contains(message.Id))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<ChatMessage?> GetLatestBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ChatMessages
            .Where(message => message.ChatSessionId == sessionId)
            .OrderByDescending(message => message.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<ChatMessage?> GetLatestBySessionAndSenderAsync(
        Guid sessionId,
        string senderType,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ChatMessages
            .Where(message => message.ChatSessionId == sessionId && message.SenderType == senderType)
            .OrderByDescending(message => message.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ChatMessages.CountAsync(message => message.ChatSessionId == sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetLatestMessageIdBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var latestMessageId = await _dbContext.ChatMessages
            .Where(message => message.ChatSessionId == sessionId)
            .OrderByDescending(message => message.CreatedAt)
            .Select(message => message.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return latestMessageId == Guid.Empty ? null : latestMessageId;
    }

    /// <inheritdoc />
    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.ChatMessages.AddAsync(message, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        _dbContext.ChatMessages.Update(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        _dbContext.ChatMessages.Remove(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteRangeAsync(IReadOnlyCollection<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var messageIds = messages
            .Select(message => message.Id)
            .ToHashSet();

        var messagesWithInternalParentReferences = messages
            .Where(message => message.ParentMessageId.HasValue && messageIds.Contains(message.ParentMessageId.Value))
            .ToList();

        if (messagesWithInternalParentReferences.Count > 0)
        {
            foreach (var message in messagesWithInternalParentReferences)
            {
                message.ParentMessageId = null;
                message.ParentMessage = null;
            }

            _dbContext.ChatMessages.UpdateRange(messagesWithInternalParentReferences);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _dbContext.ChatMessages.RemoveRange(messages);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
