namespace DevNexus.Core.Abstractions;

/// <summary>
/// Persistence boundary for user integrations.
/// </summary>
public interface IUserIntegrationStore
{
    Task<List<UserIntegration>> ListByUserAsync(
        Guid userId,
        IntegrationType? type = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<List<UserIntegration>> ListAllAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<UserIntegration?> GetByIdAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default);

    Task<UserIntegration?> GetDefaultAsync(
        Guid userId,
        IntegrationType type,
        CancellationToken cancellationToken = default);

    Task<List<UserIntegration>> ListDefaultsByTypeAsync(
        Guid userId,
        IntegrationType type,
        CancellationToken cancellationToken = default);

    Task AddAsync(UserIntegration integration, CancellationToken cancellationToken = default);

    Task RemoveAsync(UserIntegration integration, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
