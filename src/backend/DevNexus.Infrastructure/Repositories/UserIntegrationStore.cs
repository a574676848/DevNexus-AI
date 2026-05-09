using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// EF-backed user integration store.
/// </summary>
public class UserIntegrationStore : IUserIntegrationStore
{
    private readonly ApplicationDbContext _dbContext;
    private DbSet<UserIntegration> UserIntegrations => _dbContext.Set<UserIntegration>();

    public UserIntegrationStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<UserIntegration>> ListByUserAsync(
        Guid userId,
        IntegrationType? type = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = UserIntegrations.Where(i => i.UserId == userId);

        if (type.HasValue)
        {
            query = query.Where(i => i.IntegrationType == type.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        return query
            .OrderByDescending(i => i.IsDefault)
            .ThenByDescending(i => i.LastUsedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<UserIntegration>> ListAllAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var query = UserIntegrations.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(i => i.UserId == userId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(i => i.IntegrationType == type.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        return query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<UserIntegration?> GetByIdAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
        => UserIntegrations.FirstOrDefaultAsync(
            i => i.Id == integrationId && i.UserId == userId,
            cancellationToken);

    public Task<UserIntegration?> GetDefaultAsync(
        Guid userId,
        IntegrationType type,
        CancellationToken cancellationToken = default)
        => UserIntegrations
            .Where(i => i.UserId == userId && i.IntegrationType == type && i.IsActive)
            .OrderByDescending(i => i.IsDefault)
            .ThenByDescending(i => i.LastUsedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<UserIntegration>> ListDefaultsByTypeAsync(
        Guid userId,
        IntegrationType type,
        CancellationToken cancellationToken = default)
        => UserIntegrations
            .Where(i => i.UserId == userId && i.IntegrationType == type && i.IsDefault)
            .ToListAsync(cancellationToken);

    public Task AddAsync(UserIntegration integration, CancellationToken cancellationToken = default)
        => UserIntegrations.AddAsync(integration, cancellationToken).AsTask();

    public Task RemoveAsync(UserIntegration integration, CancellationToken cancellationToken = default)
    {
        UserIntegrations.Remove(integration);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
