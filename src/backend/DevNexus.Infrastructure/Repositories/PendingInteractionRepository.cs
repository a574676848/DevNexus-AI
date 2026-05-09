using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 挂起交互仓储实现。
/// </summary>
public class PendingInteractionRepository : IPendingInteractionRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public PendingInteractionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<PendingInteraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.PendingInteractions
            .Include(interaction => interaction.ChatSession)
            .FirstOrDefaultAsync(interaction => interaction.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingInteraction>> GetActiveBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PendingInteractions
            .Where(interaction =>
                interaction.SessionId == sessionId &&
                interaction.Status == PendingInteractionStatus.Pending)
            .OrderByDescending(interaction => interaction.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingInteraction>> GetExpiredPendingAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PendingInteractions
            .Include(interaction => interaction.ChatSession)
            .Where(interaction =>
                interaction.Status == PendingInteractionStatus.Pending
                && interaction.ExpiresAt.HasValue
                && interaction.ExpiresAt.Value <= utcNow)
            .OrderBy(interaction => interaction.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(PendingInteraction interaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.PendingInteractions.AddAsync(interaction, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(PendingInteraction interaction, CancellationToken cancellationToken = default)
    {
        _dbContext.PendingInteractions.Update(interaction);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> UpdateActiveStatusBySessionIdAsync(
        Guid sessionId,
        PendingInteractionStatus fromStatus,
        PendingInteractionStatus toStatus,
        CancellationToken cancellationToken = default)
    {
        var interactions = await _dbContext.PendingInteractions
            .Where(interaction => interaction.SessionId == sessionId && interaction.Status == fromStatus)
            .ToListAsync(cancellationToken);

        if (interactions.Count == 0)
        {
            return 0;
        }

        foreach (var interaction in interactions)
        {
            interaction.Status = toStatus;
            interaction.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return interactions.Count;
    }
}
