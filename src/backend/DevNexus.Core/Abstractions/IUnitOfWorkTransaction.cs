namespace DevNexus.Core.Abstractions;

/// <summary>
/// Minimal transaction abstraction for Core services.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
