namespace DevNexus.Core.Abstractions;

/// <summary>
/// Creates unit-of-work transactions backed by the persistence provider.
/// </summary>
public interface IUnitOfWorkTransactionFactory
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
