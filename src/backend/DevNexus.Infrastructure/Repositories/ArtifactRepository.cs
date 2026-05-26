using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// Artifact 仓储实现。
/// </summary>
public class ArtifactRepository : IArtifactRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ArtifactRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        await _dbContext.Artifacts.AddAsync(artifact, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Artifact?> GetByIdAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Artifacts
            .FirstOrDefaultAsync(artifact => artifact.Id == artifactId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Artifact>> ListByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Artifacts
            .Where(artifact => artifact.MessageId == messageId)
            .OrderBy(artifact => artifact.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Artifact>> ListBySessionAsync(
        Guid sessionId,
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Artifacts
            .Where(artifact => artifact.SessionId == sessionId
                || (artifact.MessageId != null && messageIds.Contains(artifact.MessageId.Value)))
            .OrderBy(artifact => artifact.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        _dbContext.Artifacts.Update(artifact);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var artifact = await _dbContext.Artifacts
            .FirstOrDefaultAsync(entity => entity.Id == artifactId, cancellationToken);

        if (artifact == null)
        {
            return false;
        }

        _dbContext.Artifacts.Remove(artifact);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> DeleteBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var artifacts = await _dbContext.Artifacts
            .Where(artifact => artifact.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        if (artifacts.Count == 0)
        {
            return 0;
        }

        _dbContext.Artifacts.RemoveRange(artifacts);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return artifacts.Count;
    }

    /// <inheritdoc />
    public Task<int> LinkToMessageAsync(
        IReadOnlyCollection<Guid> artifactIds,
        Guid messageId,
        Guid sessionId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Artifacts
            .Where(artifact => artifactIds.Contains(artifact.Id)
                && (artifact.SessionId == sessionId || artifact.MessageId == messageId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(artifact => artifact.MessageId, messageId)
                    .SetProperty(artifact => artifact.UpdatedAt, updatedAtUtc),
                cancellationToken);
    }
}
