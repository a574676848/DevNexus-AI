using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 排队聊天消息仓储实现。
/// 负责排队消息的持久化、状态流转与队列消费相关操作。
/// </summary>
public class QueuedChatMessageRepository : IQueuedChatMessageRepository
{
    private readonly ApplicationDbContext _dbContext;

    public QueuedChatMessageRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(QueuedChatMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.QueuedChatMessages.AddAsync(message, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueuedChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.QueuedChatMessages
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedChatMessage>> ListBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueuedChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedChatMessage>> ListBySessionAndStatusAsync(
        Guid sessionId,
        QueuedMessageStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueuedChatMessages
            .Where(m => m.ChatSessionId == sessionId && m.Status == status)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 使用数据库级原子 UPDATE 操作实现抢占式出队，防止并发派发。
    /// 原理：先用 SELECT FOR UPDATE SKIP LOCKED 锁定第一条 Pending 消息，
    /// 然后将其状态更新为 Dispatching。其他并发请求会跳过已被锁定的行。
    /// PostgreSQL 原生支持此模式。
    /// </remarks>
    public async Task<QueuedChatMessage?> TryDequeueNextAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // 使用原生 SQL 实现原子抢占：
        // 1. 用 FOR UPDATE SKIP LOCKED 锁定第一条 Pending 消息
        // 2. 将状态更新为 Dispatching（防止重复派发）
        // 3. 返回被锁定的消息
        var sql = @"
            WITH cte AS (
                SELECT ""Id""
                FROM ""QueuedChatMessages""
                WHERE ""ChatSessionId"" = @p0
                  AND ""Status"" = 0
                  AND ""IsDeleted"" = FALSE
                ORDER BY ""SequenceNumber"" ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            UPDATE ""QueuedChatMessages""
            SET ""Status"" = 1, ""StartedAt"" = @p1, ""UpdatedAt"" = @p1
            FROM cte
            WHERE ""QueuedChatMessages"".""Id"" = cte.""Id""
            RETURNING ""QueuedChatMessages"".""Id""";

        var nowUtc = DateTime.UtcNow;
        var ids = await _dbContext.Database
            .SqlQueryRaw<Guid>(sql, sessionId, nowUtc)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return null;
        }

        var dequeuedId = ids[0];
        // 重新查询获取完整实体
        return await _dbContext.QueuedChatMessages
            .FirstOrDefaultAsync(m => m.Id == dequeuedId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(QueuedChatMessage message, CancellationToken cancellationToken = default)
    {
        message.UpdatedAt = DateTime.UtcNow;
        _dbContext.QueuedChatMessages.Update(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CancelAllPendingBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var cancelledCount = await _dbContext.QueuedChatMessages
            .Where(m => m.ChatSessionId == sessionId && m.Status == QueuedMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, QueuedMessageStatus.Cancelled)
                .SetProperty(m => m.CancelledAt, nowUtc)
                .SetProperty(m => m.UpdatedAt, nowUtc),
                cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return cancelledCount;
    }

    /// <inheritdoc />
    public Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.QueuedChatMessages
            .CountAsync(m => m.ChatSessionId == sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountPendingBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.QueuedChatMessages
            .CountAsync(m => m.ChatSessionId == sessionId && m.Status == QueuedMessageStatus.Pending, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(QueuedChatMessage message, CancellationToken cancellationToken = default)
    {
        _dbContext.QueuedChatMessages.Remove(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetMaxSequenceNumberAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var maxSeq = await _dbContext.QueuedChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .MaxAsync(m => (int?)m.SequenceNumber, cancellationToken);

        return maxSeq ?? 0;
    }
}
