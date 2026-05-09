using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 更新发布版本仓储实现。
/// </summary>
public class UpdateReleaseRepository : IUpdateReleaseRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateReleaseRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpdateRelease>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.UpdateReleases
            .Include(r => r.Artifacts)
            .OrderByDescending(r => r.PublishedAt ?? r.CreatedAt)
            .ThenByDescending(r => r.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpdateRelease?> GetByIdAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UpdateReleases
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(r => r.Id == releaseId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpdateRelease> SaveAsync(UpdateRelease release, CancellationToken cancellationToken = default)
    {
        if (release.Id == Guid.Empty)
        {
            release.Id = Guid.NewGuid();
            release.CreatedAt = DateTime.UtcNow;
            release.UpdatedAt = DateTime.UtcNow;
            await _dbContext.UpdateReleases.AddAsync(release, cancellationToken);
        }
        else
        {
            var existing = await _dbContext.UpdateReleases.FirstOrDefaultAsync(r => r.Id == release.Id, cancellationToken)
                ?? throw new InvalidOperationException($"发布版本 {release.Id} 不存在");

            existing.Version = release.Version;
            existing.Channel = release.Channel;
            existing.Title = release.Title;
            existing.ReleaseNotes = release.ReleaseNotes;
            existing.Status = release.Status;
            existing.PublishedAt = release.PublishedAt;
            existing.UpdatedAt = DateTime.UtcNow;
            release = existing;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return release;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(UpdateRelease release, CancellationToken cancellationToken = default)
    {
        _dbContext.UpdateReleases.Remove(release);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReplaceArtifactsAsync(
        Guid releaseId,
        IReadOnlyCollection<UpdateReleaseArtifact> artifacts,
        CancellationToken cancellationToken = default)
    {
        var existingArtifacts = await _dbContext.UpdateReleaseArtifacts
            .Where(a => a.ReleaseId == releaseId)
            .ToListAsync(cancellationToken);

        _dbContext.UpdateReleaseArtifacts.RemoveRange(existingArtifacts);

        foreach (var artifact in artifacts)
        {
            artifact.Id = artifact.Id == Guid.Empty ? Guid.NewGuid() : artifact.Id;
            artifact.ReleaseId = releaseId;
            artifact.CreatedAt = artifact.CreatedAt == default ? DateTime.UtcNow : artifact.CreatedAt;
            artifact.UpdatedAt = DateTime.UtcNow;
            artifact.IsDeleted = false;
            artifact.DeletedAt = null;
        }

        await _dbContext.UpdateReleaseArtifacts.AddRangeAsync(artifacts, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpdateRelease?> GetPreviousPublishedReleaseAsync(
        string channel,
        Guid excludedReleaseId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UpdateReleases
            .Include(r => r.Artifacts)
            .Where(r => r.Channel == channel)
            .Where(r => r.Status == UpdateReleaseStatus.Published)
            .Where(r => r.Id != excludedReleaseId)
            .OrderByDescending(r => r.PublishedAt ?? r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
