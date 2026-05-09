using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// CLI 审批授权仓储实现。
/// </summary>
public sealed class CliApprovalGrantRepository : ICliApprovalGrantRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliApprovalGrantRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<CliApprovalGrant?> GetActiveGrantAsync(
        string sessionScopeKey,
        CliApprovalGrantScope scope,
        string matchValue,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<CliApprovalGrant>()
            .Where(grant => grant.SessionScopeKey == sessionScopeKey
                && grant.Scope == scope
                && grant.MatchValue == matchValue
                && grant.ConsumedAt == null)
            .OrderByDescending(grant => grant.ApprovedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(CliApprovalGrant grant, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<CliApprovalGrant>().AddAsync(grant, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(CliApprovalGrant grant, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<CliApprovalGrant>().Update(grant);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
