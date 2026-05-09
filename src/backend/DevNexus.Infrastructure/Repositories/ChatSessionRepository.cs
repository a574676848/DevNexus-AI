using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 聊天会话仓储实现。
/// </summary>
public class ChatSessionRepository : IChatSessionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ChatSessionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<ChatSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ChatSessions
            .Include(session => session.LLMProvider)
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ChatSession?> GetByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ChatSessions
            .Include(session => session.LLMProvider)
            .FirstOrDefaultAsync(
                session => session.Id == sessionId && session.UserId == userId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatSession>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChatSessions
            .Include(session => session.Messages.OrderByDescending(message => message.CreatedAt).Take(1))
            .Include(session => session.LLMProvider)
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        await _dbContext.ChatSessions.AddAsync(session, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        _dbContext.ChatSessions.Update(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        _dbContext.ChatSessions.Remove(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetUserIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var userId = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(session => session.Id == sessionId)
            .Select(session => session.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return userId == Guid.Empty ? null : userId;
    }
}
