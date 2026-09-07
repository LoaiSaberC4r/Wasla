using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests.Integration.Persistence;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private SqliteTestDatabase(SqliteConnection connection)
    {
        Connection = connection;
    }

    public SqliteConnection Connection { get; }

    public static async Task<SqliteTestDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new SqliteTestDatabase(connection);
    }

    public DbContextOptions<TContext> CreateOptions<TContext>(
        Action<DbContextOptionsBuilder<TContext>>? configure = null)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(Connection);

        configure?.Invoke(builder);
        return builder.Options;
    }

    public async Task EnsureCreatedAsync<TContext>(
        Func<DbContextOptions<TContext>, TContext> createContext,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        await using var context = createContext(CreateOptions<TContext>());
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}
