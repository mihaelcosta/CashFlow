namespace CashFlow.Domain.Accounts;

public sealed class Transaction
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public int Sequence { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private Transaction()
    {
    }

    internal Transaction(
        Guid accountId,
        int sequence,
        TransactionType type,
        decimal amount,
        decimal balanceAfter,
        string? description,
        DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        AccountId = accountId;
        Sequence = sequence;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Description = description;
        OccurredAt = occurredAt;
    }
}
