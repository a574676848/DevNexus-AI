using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// CLI 执行快照仓储实现。
/// </summary>
public class CliExecCheckpointRepository : ICliExecCheckpointRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliExecCheckpointRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CliExecCheckpoint>> GetActiveBySessionKeyAsync(
        string sessionKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<CliExecCheckpoint>()
            .Where(checkpoint => checkpoint.SessionKey == sessionKey && checkpoint.Status == CliExecCheckpointStatus.Created)
            .OrderByDescending(checkpoint => checkpoint.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliExecCheckpoint?> GetLatestActiveBySessionKeyAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<CliExecCheckpoint>()
            .Where(checkpoint => checkpoint.SessionKey == sessionKey && checkpoint.Status == CliExecCheckpointStatus.Created)
            .OrderByDescending(checkpoint => checkpoint.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(CliExecCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<CliExecCheckpoint>().AddAsync(checkpoint, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(CliExecCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<CliExecCheckpoint>().Update(checkpoint);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateRangeAsync(IEnumerable<CliExecCheckpoint> checkpoints, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<CliExecCheckpoint>().UpdateRange(checkpoints);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
