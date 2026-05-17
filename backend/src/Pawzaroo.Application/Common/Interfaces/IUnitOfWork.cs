namespace Pawzaroo.Application.Common.Interfaces;

/// <summary>
/// Thin wrapper over DbContext for handlers that need explicit transaction
/// boundaries spanning multiple SaveChanges calls or multiple aggregates.
/// Most CQRS handlers don't need this — they hit IApplicationDbContext directly
/// and SaveChangesAsync is the implicit transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
}

public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
