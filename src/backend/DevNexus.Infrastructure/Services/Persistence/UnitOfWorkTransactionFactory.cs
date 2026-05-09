using DevNexus.Core.Abstractions;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace DevNexus.Infrastructure.Services.Persistence;

public class UnitOfWorkTransactionFactory : IUnitOfWorkTransactionFactory
{
    private readonly ApplicationDbContext _dbContext;

    public UnitOfWorkTransactionFactory(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWorkTransaction(transaction);
    }

    private sealed class EfUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfUnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
