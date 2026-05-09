namespace DevNexus.Core.Abstractions;

/// <summary>
/// Executes operations inside the provider-specific resilient execution strategy.
/// </summary>
public interface IExecutionStrategyExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
