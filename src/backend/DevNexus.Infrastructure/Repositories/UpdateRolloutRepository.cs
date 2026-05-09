using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 更新投放规则仓储实现。
/// </summary>
public class UpdateRolloutRepository : IUpdateRolloutRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateRolloutRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpdateRollout>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.UpdateRollouts
            .Include(r => r.Release)
            .OrderByDescending(r => r.Enabled)
            .ThenByDescending(r => r.Priority)
            .ThenByDescending(r => r.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpdateRollout?> GetByIdAsync(Guid rolloutId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UpdateRollouts
            .Include(r => r.Release)
            .FirstOrDefaultAsync(r => r.Id == rolloutId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpdateRollout> SaveAsync(UpdateRollout rollout, CancellationToken cancellationToken = default)
    {
        if (rollout.Id == Guid.Empty)
        {
            rollout.Id = Guid.NewGuid();
            rollout.CreatedAt = DateTime.UtcNow;
            rollout.UpdatedAt = DateTime.UtcNow;
            await _dbContext.UpdateRollouts.AddAsync(rollout, cancellationToken);
        }
        else
        {
            var existing = await _dbContext.UpdateRollouts.FirstOrDefaultAsync(r => r.Id == rollout.Id, cancellationToken)
                ?? throw new InvalidOperationException($"投放规则 {rollout.Id} 不存在");

            existing.ReleaseId = rollout.ReleaseId;
            existing.Platform = rollout.Platform;
            existing.Architecture = rollout.Architecture;
            existing.Channel = rollout.Channel;
            existing.MinimumSupportedVersion = rollout.MinimumSupportedVersion;
            existing.ForceUpdate = rollout.ForceUpdate;
            existing.RolloutPercent = rollout.RolloutPercent;
            existing.AudienceRule = rollout.AudienceRule;
            existing.StartsAt = rollout.StartsAt;
            existing.EndsAt = rollout.EndsAt;
            existing.Priority = rollout.Priority;
            existing.Enabled = rollout.Enabled;
            existing.KillSwitchEnabled = rollout.KillSwitchEnabled;
            existing.UpdatedAt = DateTime.UtcNow;
            rollout = existing;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return rollout;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(UpdateRollout rollout, CancellationToken cancellationToken = default)
    {
        _dbContext.UpdateRollouts.Remove(rollout);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HasAnyByReleaseIdAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UpdateRollouts.AnyAsync(r => r.ReleaseId == releaseId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpdateRollout>> GetManifestCandidatesAsync(
        string platform,
        string architecture,
        string channel,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UpdateRollouts
            .Include(r => r.Release)
                .ThenInclude(r => r!.Artifacts)
            .Where(r => r.Enabled)
            .Where(r => !r.KillSwitchEnabled)
            .Where(r => r.Platform == platform)
            .Where(r => r.Channel == channel)
            .Where(r => r.StartsAt <= utcNow)
            .Where(r => !r.EndsAt.HasValue || r.EndsAt > utcNow)
            .Where(r => r.Architecture == architecture || r.Architecture == "any" || r.Architecture == "*")
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}
