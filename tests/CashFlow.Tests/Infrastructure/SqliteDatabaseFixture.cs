using CashFlow.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Tests.Infrastructure;

public sealed class SqliteDatabaseFixture : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<CashFlowDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<CashFlowDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public CashFlowDbContext CreateContext() => new(_options);

    public async Task DisposeAsync()
        => await _connection.DisposeAsync();
}
