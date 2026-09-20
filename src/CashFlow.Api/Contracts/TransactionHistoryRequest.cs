using System.ComponentModel.DataAnnotations;
using CashFlow.Domain.Accounts;

namespace CashFlow.Api.Contracts;

public sealed record TransactionHistoryRequest
{
    public TransactionType? Type { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
