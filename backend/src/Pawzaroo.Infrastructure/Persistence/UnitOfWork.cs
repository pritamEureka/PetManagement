using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    public UnitOfWork(ApplicationDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);
        return new TransactionScope(tx);
    }

    /// <summary>
    /// Use the EF execution strategy so retries on transient PG failures also
    /// retry the whole transaction body — required when retries are enabled.
    /// </summary>
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        // EF Core 9's generic IExecutionStrategy.ExecuteAsync requires a
        // verifySucceeded callback. The non-generic extension overload below
        // accepts a plain Func<CancellationToken, Task> and handles the
        // book-keeping internally.
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async (CancellationToken token) =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(token);
            try
            {
                await work(token);
                await _db.SaveChangesAsync(token);
                await tx.CommitAsync(token);
            }
            catch
            {
                await tx.RollbackAsync(token);
                throw;
            }
        }, ct);
    }

    private sealed class TransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _tx;
        public TransactionScope(IDbContextTransaction tx) => _tx = tx;
        public Task CommitAsync(CancellationToken ct = default) => _tx.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => _tx.RollbackAsync(ct);
        public ValueTask DisposeAsync() => _tx.DisposeAsync();
    }
}
