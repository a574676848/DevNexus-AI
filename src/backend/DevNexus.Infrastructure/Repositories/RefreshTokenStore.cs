using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// EF-backed refresh token store.
/// </summary>
public class RefreshTokenStore : IRefreshTokenStore
{
    private readonly ApplicationDbContext _dbContext;
    private DbSet<RefreshToken> RefreshTokens => _dbContext.Set<RefreshToken>();

    public RefreshTokenStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
    }

    public Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        return RefreshTokens.AddAsync(refreshToken, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
