using CashFlow.Domain.Accounts;

namespace CashFlow.Application.Accounts.Results;

public sealed record TransactionResult(
    Guid Id,
    Guid AccountId,
    int Sequence,
    TransactionType Type,
    decimal Amount,
    decimal BalanceAfter,
    string? Description,
    DateTimeOffset OccurredAt)
{
    public static TransactionResult From(Transaction transaction)
        => new(
            transaction.Id,
            transaction.AccountId,
            transaction.Sequence,
            transaction.Type,
            transaction.Amount,
            transaction.BalanceAfter,
            transaction.Description,
            transaction.OccurredAt);
}
