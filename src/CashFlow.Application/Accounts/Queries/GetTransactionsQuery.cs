using CashFlow.Domain.Accounts;

namespace CashFlow.Application.Accounts.Queries;

public sealed record GetTransactionsQuery(
    Guid AccountId,
    TransactionType? Type,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);
