using CashFlow.Domain.Accounts;
using CashFlow.Domain.Accounts.Exceptions;
using CashFlow.Domain.Common;
using CashFlow.Tests.Builders;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Tests.Infrastructure;

public sealed class OptimisticConcurrencyTests(SqliteDatabaseFixture database) : IClassFixture<SqliteDatabaseFixture>
{
    private static readonly DateTimeOffset Now = AccountBuilder.DefaultDate;

    [Fact]
    public async Task TwoUnitsOfWork_WithdrawingFromTheSameSnapshot_OnlyTheFirstCommitWins()
    {
        // Arrange
        var accountId = await SeedAccountAsync(balance: 100m);

        await using var firstUnit = database.CreateContext();
        await using var secondUnit = database.CreateContext();

        var accountSeenByFirst = await firstUnit.Accounts.SingleAsync(a => a.Id == accountId);
        var accountSeenBySecond = await secondUnit.Accounts.SingleAsync(a => a.Id == accountId);

        // Act
        firstUnit.Transactions.Add(accountSeenByFirst.Withdraw(80m, "Supplier A", Now));
        await firstUnit.CommitAsync();

        secondUnit.Transactions.Add(accountSeenBySecond.Withdraw(80m, "Supplier B", Now));
        var secondCommit = () => secondUnit.CommitAsync();

        // Assert
        var conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(secondCommit);
        Assert.IsType<DbUpdateConcurrencyException>(conflict.InnerException);

        await using var verification = database.CreateContext();
        var persisted = await verification.Accounts.SingleAsync(a => a.Id == accountId);
        var transactions = await verification.Transactions.Where(t => t.AccountId == accountId).ToListAsync();

        Assert.Equal(20m, persisted.Balance);
        Assert.Equal(2, persisted.Version);
        Assert.Equal(2, transactions.Count);
        Assert.DoesNotContain(transactions, t => t.Description == "Supplier B");
    }

    [Fact]
    public async Task AfterAConflict_TheUnitOfWorkIsClean_AndRetryingOnFreshStateAppliesDomainRules()
    {
        // Arrange
        var accountId = await SeedAccountAsync(balance: 100m);

        await using var competitor = database.CreateContext();
        await using var retrying = database.CreateContext();

        var competitorView = await competitor.Accounts.SingleAsync(a => a.Id == accountId);
        var staleView = await retrying.Accounts.SingleAsync(a => a.Id == accountId);

        competitor.Transactions.Add(competitorView.Withdraw(70m, null, Now));
        await competitor.CommitAsync();

        retrying.Transactions.Add(staleView.Withdraw(50m, null, Now));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => retrying.CommitAsync());

        // Act
        var trackedAfterConflict = retrying.ChangeTracker.Entries().Count();
        var freshView = await retrying.Accounts.SingleAsync(a => a.Id == accountId);
        var sameAmountAgain = Assert.Throws<InsufficientFundsException>(() => freshView.Withdraw(50m, null, Now));
        retrying.Transactions.Add(freshView.Withdraw(30m, null, Now));
        await retrying.CommitAsync();

        // Assert
        Assert.Equal(0, trackedAfterConflict);
        Assert.Equal(30m, sameAmountAgain.Balance);

        await using var verification = database.CreateContext();
        var persisted = await verification.Accounts.SingleAsync(a => a.Id == accountId);
        Assert.Equal(0m, persisted.Balance);
        Assert.Equal(3, persisted.Version);
    }

    [Fact]
    public async Task SequentialCommits_FromDifferentUnitsOfWork_IncrementVersionMonotonically()
    {
        // Arrange
        var accountId = await SeedAccountAsync(balance: 0m);

        // Act
        for (var i = 1; i <= 5; i++)
        {
            await using var unit = database.CreateContext();
            var account = await unit.Accounts.SingleAsync(a => a.Id == accountId);
            unit.Transactions.Add(account.Deposit(10m, $"Deposit {i}", Now.AddMinutes(i)));
            await unit.CommitAsync();
        }

        // Assert
        await using var verification = database.CreateContext();
        var persisted = await verification.Accounts.SingleAsync(a => a.Id == accountId);

        Assert.Equal(50m, persisted.Balance);
        Assert.Equal(5, persisted.Version);
    }

    private async Task<Guid> SeedAccountAsync(decimal balance)
    {
        await using var context = database.CreateContext();
        var account = new Account("Seed", Now);

        context.Accounts.Add(account);
        if (balance > 0)
        {
            context.Transactions.Add(account.Deposit(balance, "Opening balance", Now));
        }

        await context.CommitAsync();
        return account.Id;
    }
}
