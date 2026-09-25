using System.Data;
using System.Data.Common;
using Npgsql;

namespace ParsingEndpoint.Database;

public interface IDapperSession : IAsyncDisposable
{
    DbTransaction? Transaction { get; }
    Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken);
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
}

public sealed class DapperSession(string connectionString) : IDapperSession
{
    private readonly NpgsqlConnection _connection = new(connectionString);

    public DbTransaction? Transaction { get; private set; }

    public async Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
        {
            throw new InvalidOperationException("A database transaction is already active.");
        }

        var connection = await GetOpenConnectionAsync(cancellationToken);
        Transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = Transaction
            ?? throw new InvalidOperationException("There is no active database transaction.");

        try
        {
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await transaction.DisposeAsync();
            Transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        if (Transaction is null)
        {
            return;
        }

        try
        {
            await Transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }

        await _connection.DisposeAsync();
    }
}
