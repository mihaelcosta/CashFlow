using CashFlow.Application.Accounts.Queries;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;
using CashFlow.Domain.Accounts;
using CashFlow.Infrastructure.Persistence.Queries;
using CashFlow.Tests.Builders;

namespace CashFlow.Tests.Infrastructure;

public sealed class TransactionHistoryReaderTests(SqliteDatabaseFixture database) : IClassFixture<SqliteDatabaseFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset September1 = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private Guid _accountId;

    public async Task InitializeAsync()
    {
        await using var context = database.CreateContext();

        var account = AccountBuilder.Empty();
        var other = AccountBuilder.Empty();
        context.Accounts.AddRange(account, other);

        context.Transactions.AddRange(
            account.Deposit(1_000m, "Sales week 1", September1.AddDays(2)),
            account.Withdraw(200m, "Rent", September1.AddDays(5)),
            account.Deposit(1_500m, "Sales week 2", September1.AddDays(9)),
            account.Withdraw(700m, "Payroll", September1.AddDays(15)),
            account.Deposit(300m, "Refund", September1.AddDays(28)),
            other.Deposit(999m, "Not mine", September1.AddDays(10)));

        await context.CommitAsync();

        _accountId = account.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Read_ReturnsOnlyTheRequestedAccount_NewestFirst()
    {
        // Arrange
        var query = Query(_accountId);

        // Act
        var page = await Read(query);

        // Assert
        Assert.Equal(5, page.TotalCount);
        Assert.All(page.Items, t => Assert.Equal(_accountId, t.AccountId));
        Assert.Equal(["Refund", "Payroll", "Sales week 2", "Rent", "Sales week 1"], page.Items.Select(t => t.Description));
    }

    [Fact]
    public async Task Read_FiltersByType()
    {
        // Arrange
        var query = Query(_accountId, type: TransactionType.Debit);

        // Act
        var page = await Read(query);

        // Assert
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, t => Assert.Equal(TransactionType.Debit, t.Type));
    }

    [Fact]
    public async Task Read_FiltersByInclusiveDateRange()
    {
        // Arrange
        var query = Query(_accountId, from: September1.AddDays(5), to: September1.AddDays(15));

        // Act
        var page = await Read(query);

        // Assert
        Assert.Equal(["Payroll", "Sales week 2", "Rent"], page.Items.Select(t => t.Description));
    }

    [Fact]
    public async Task Read_CombinesTypeAndDateFilters()
    {
        // Arrange
        var query = Query(_accountId, type: TransactionType.Credit, from: September1.AddDays(3));

        // Act
        var page = await Read(query);

        // Assert
        Assert.Equal(["Refund", "Sales week 2"], page.Items.Select(t => t.Description));
    }

    [Fact]
    public async Task Read_Paginates_AndReportsTotals()
    {
        // Arrange
        var firstPage = Query(_accountId, page: 1, pageSize: 2);
        var lastPage = Query(_accountId, page: 3, pageSize: 2);

        // Act
        var first = await Read(firstPage);
        var last = await Read(lastPage);

        // Assert
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(["Refund", "Payroll"], first.Items.Select(t => t.Description));
        Assert.Equal(["Sales week 1"], last.Items.Select(t => t.Description));
    }

    [Fact]
    public async Task Read_PageBeyondTheEnd_ReturnsEmptyItems_ButKeepsTotals()
    {
        // Arrange
        var query = Query(_accountId, page: 10, pageSize: 20);

        // Act
        var page = await Read(query);

        // Assert
        Assert.Empty(page.Items);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task Read_ForAccountWithoutMovements_ReturnsEmptyPage()
    {
        // Arrange
        await using var context = database.CreateContext();
        var lonely = AccountBuilder.Empty();
        context.Accounts.Add(lonely);
        await context.CommitAsync();

        // Act
        var page = await Read(Query(lonely.Id));

        // Assert
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    private static GetTransactionsQuery Query(
        Guid accountId,
        TransactionType? type = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20)
        => new(accountId, type, from, to, page, pageSize);

    private async Task<PagedResult<TransactionResult>> Read(GetTransactionsQuery query)
    {
        await using var context = database.CreateContext();
        return await new TransactionHistoryReader(context).ReadAsync(query);
    }
}
