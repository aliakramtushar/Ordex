using Ordex.Core.Abstractions;

namespace Ordex.Infrastructure.Data;

/// <summary>
/// Wraps work in a database transaction: commit when it finishes, roll back
/// on any exception (ACID). A nested call simply joins the running transaction.
/// </summary>
public sealed class UnitOfWork(DbSession session) : IUnitOfWork
{
    public Task ExecuteAsync(Func<Task> work, CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            await work();
            return true;
        }, ct);

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        if (session.Transaction is not null)
            return await work();

        await session.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            await session.CommitAsync(ct);
            return result;
        }
        catch
        {
            await session.RollbackAsync();
            throw;
        }
    }
}
