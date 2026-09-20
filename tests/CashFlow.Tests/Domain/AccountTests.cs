using CashFlow.Domain.Accounts;
using CashFlow.Domain.Accounts.Exceptions;
using CashFlow.Tests.Builders;

namespace CashFlow.Tests.Domain;

public sealed class AccountTests
{
    private static readonly DateTimeOffset Now = AccountBuilder.DefaultDate;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        // Act
        var act = () => new Account(name, Now);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_TrimsName_AndStartsWithZeroBalance()
    {
        // Act
        var account = new Account("  Acme Ltda  ", Now);

        // Assert
        Assert.Equal("Acme Ltda", account.Name);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(0, account.Version);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Fact]
    public void Deposit_IncreasesBalance_AndRecordsCredit()
    {
        // Arrange
        var account = AccountBuilder.Empty();

        // Act
        var transaction = account.Deposit(1_500.50m, "Invoice #1042", Now);

        // Assert
        Assert.Equal(1_500.50m, account.Balance);
        Assert.Equal(TransactionType.Credit, transaction.Type);
        Assert.Equal(1_500.50m, transaction.Amount);
        Assert.Equal(1_500.50m, transaction.BalanceAfter);
        Assert.Equal(account.Id, transaction.AccountId);
        Assert.Equal("Invoice #1042", transaction.Description);
        Assert.Equal(Now, transaction.OccurredAt);
        Assert.NotEqual(Guid.Empty, transaction.Id);
    }

    [Fact]
    public void Withdraw_WithSufficientFunds_DecreasesBalance_AndRecordsDebit()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(1_000m);

        // Act
        var transaction = account.Withdraw(350m, "Supplier payment", Now);

        // Assert
        Assert.Equal(650m, account.Balance);
        Assert.Equal(TransactionType.Debit, transaction.Type);
        Assert.Equal(350m, transaction.Amount);
        Assert.Equal(650m, transaction.BalanceAfter);
    }

    [Fact]
    public void Withdraw_ExactBalance_LeavesZero_AndNextWithdrawalIsRejected()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(1_000m);

        // Act
        account.Withdraw(1_000m, "Full payout", Now);
        var act = () => account.Withdraw(0.01m, null, Now);

        // Assert
        Assert.Equal(0m, account.Balance);
        Assert.Throws<InsufficientFundsException>(act);
    }

    [Fact]
    public void Withdraw_OneCentAboveBalance_Throws_AndLeavesStateUntouched()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(100m);
        var versionBefore = account.Version;

        // Act
        var exception = Assert.Throws<InsufficientFundsException>(() => account.Withdraw(100.01m, null, Now));

        // Assert
        Assert.Equal(100m, exception.Balance);
        Assert.Equal(100.01m, exception.Requested);
        Assert.Equal(100m, account.Balance);
        Assert.Equal(versionBefore, account.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-1_000)]
    public void Deposit_WithNonPositiveAmount_Throws(decimal amount)
    {
        // Arrange
        var account = AccountBuilder.WithFunds(10m);

        // Act
        var exception = Assert.Throws<InvalidAmountException>(() => account.Deposit(amount, null, Now));

        // Assert
        Assert.Equal(amount, exception.Amount);
        Assert.Equal(10m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Withdraw_WithNonPositiveAmount_Throws_BeforeCheckingFunds(decimal amount)
    {
        // Arrange
        var account = AccountBuilder.Empty();

        // Act
        var act = () => account.Withdraw(amount, null, Now);

        // Assert
        Assert.Throws<InvalidAmountException>(act);
    }

    [Fact]
    public void Movements_WithFractionalCents_KeepExactDecimalPrecision()
    {
        // Arrange
        var account = AccountBuilder.Empty();

        // Act
        account.Deposit(0.10m, null, Now);
        account.Deposit(0.20m, null, Now);
        account.Withdraw(0.30m, null, Now);

        // Assert
        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public void Payroll_ConsumesBalanceSequentially_AndRejectsTheEmployeeThatDoesNotFit()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(10_000m);
        var salaries = new[] { 3_500m, 3_500m, 2_000m, 1_500m };
        var paid = new List<Transaction>();

        // Act
        foreach (var salary in salaries.Take(3))
        {
            paid.Add(account.Withdraw(salary, "Payroll", Now));
        }

        var lastOne = () => account.Withdraw(salaries[3], "Payroll", Now);

        // Assert
        Assert.Equal(1_000m, account.Balance);
        Assert.Equal([6_500m, 3_000m, 1_000m], paid.Select(t => t.BalanceAfter));
        Assert.Equal([2, 3, 4], paid.Select(t => t.Sequence));
        Assert.Throws<InsufficientFundsException>(lastOne);
    }

    [Fact]
    public void Statement_BalanceAfterOfEachMovement_IsARunningTotal()
    {
        // Arrange
        var account = AccountBuilder.Empty();
        var movements = new (TransactionType Type, decimal Amount)[]
        {
            (TransactionType.Credit, 5_000m),
            (TransactionType.Debit, 1_200m),
            (TransactionType.Credit, 800.75m),
            (TransactionType.Debit, 4_600.75m),
            (TransactionType.Credit, 0.01m)
        };

        // Act
        var statement = movements
            .Select((m, i) => m.Type == TransactionType.Credit
                ? account.Deposit(m.Amount, null, Now.AddMinutes(i))
                : account.Withdraw(m.Amount, null, Now.AddMinutes(i)))
            .ToList();

        // Assert
        var runningTotal = 0m;
        foreach (var transaction in statement)
        {
            runningTotal += transaction.Type == TransactionType.Credit ? transaction.Amount : -transaction.Amount;
            Assert.Equal(runningTotal, transaction.BalanceAfter);
        }

        Assert.Equal(0.01m, account.Balance);
        Assert.Equal(movements.Length, account.Version);
        Assert.Equal(Enumerable.Range(1, movements.Length), statement.Select(t => t.Sequence));
    }

    [Fact]
    public void Version_IncrementsOnlyOnSuccessfulMovements()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(50m);

        // Act
        account.Deposit(10m, null, Now);
        Assert.Throws<InsufficientFundsException>(() => account.Withdraw(500m, null, Now));
        Assert.Throws<InvalidAmountException>(() => account.Deposit(0m, null, Now));
        account.Withdraw(5m, null, Now);

        // Assert
        Assert.Equal(3, account.Version);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Rent  ", "Rent")]
    public void Movement_NormalizesDescription(string? input, string? expected)
    {
        // Arrange
        var account = AccountBuilder.Empty();

        // Act
        var transaction = account.Deposit(1m, input, Now);

        // Assert
        Assert.Equal(expected, transaction.Description);
    }
}
