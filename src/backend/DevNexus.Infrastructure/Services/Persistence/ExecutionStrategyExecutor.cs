using DevNexus.Core.Abstractions;
using DevNexus.Infrastructure.Models;

namespace DevNexus.Infrastructure.Services.Persistence;

public class ExecutionStrategyExecutor : IExecutionStrategyExecutor
{
    private readonly ApplicationDbContext _dbContext;

    public ExecutionStrategyExecutor(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            operation,
            static (_, op, ct) => op(ct),
            verifySucceeded: null,
            cancellationToken: cancellationToken);
    }
}
