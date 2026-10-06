using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Ordex.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Create the database and run /database/*.sql on startup.</summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>Seconds before a command is cancelled.</summary>
    public int CommandTimeout { get; set; } = 30;
}

/// <summary>
/// One database connection per HTTP request (registered as scoped).
/// Every repository in the same request shares it – and shares the open
/// transaction, which is what makes multi-table saves atomic.
/// </summary>
public sealed class DbSession(IOptions<DatabaseOptions> options) : IAsyncDisposable, IDisposable
{
    private readonly DatabaseOptions _options = options.Value;
    private SqlConnection? _connection;

    public DbTransaction? Transaction { get; private set; }

    public int CommandTimeout => _options.CommandTimeout;

    public async Task<DbConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        _connection ??= new SqlConnection(_options.ConnectionString);

        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);

        return _connection;
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (Transaction is not null)
            throw new InvalidOperationException("A transaction is already running.");

        var connection = await GetConnectionAsync(ct);
        Transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (Transaction is null) return;

        await Transaction.CommitAsync(ct);
        await DisposeTransactionAsync();
    }

    public async Task RollbackAsync()
    {
        if (Transaction is null) return;

        try
        {
            await Transaction.RollbackAsync();
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (Transaction is not null)
            await Transaction.DisposeAsync();
        Transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeTransactionAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
        _connection = null;
    }

    public void Dispose()
    {
        Transaction?.Dispose();
        Transaction = null;
        _connection?.Dispose();
        _connection = null;
    }
}
