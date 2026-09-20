using CashFlow.Domain.Accounts.Exceptions;

namespace CashFlow.Domain.Accounts;

public sealed class Account
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal Balance { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Account()
    {
        Name = string.Empty;
    }

    public Account(string name, DateTimeOffset createdAt)
        : this(Guid.CreateVersion7(), name, createdAt)
    {
    }

    public Account(Guid id, string name, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name.Trim();
        CreatedAt = createdAt;
    }

    public Transaction Deposit(decimal amount, string? description, DateTimeOffset occurredAt)
    {
        EnsurePositive(amount);

        Balance += amount;
        return Record(TransactionType.Credit, amount, description, occurredAt);
    }

    public Transaction Withdraw(decimal amount, string? description, DateTimeOffset occurredAt)
    {
        EnsurePositive(amount);

        if (Balance < amount)
        {
            throw new InsufficientFundsException(Balance, amount);
        }

        Balance -= amount;
        return Record(TransactionType.Debit, amount, description, occurredAt);
    }

    private Transaction Record(TransactionType type, decimal amount, string? description, DateTimeOffset occurredAt)
    {
        Version++;
        return new Transaction(Id, Version, type, amount, Balance, NormalizeDescription(description), occurredAt);
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidAmountException(amount);
        }
    }

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
