using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Queries;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Persistence.Queries;

internal sealed class TransactionHistoryReader(CashFlowDbContext context) : ITransactionHistoryReader
{
    public async Task<PagedResult<TransactionResult>> ReadAsync(GetTransactionsQuery query, CancellationToken cancellationToken = default)
    {
        var transactions = context.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == query.AccountId);

        if (query.Type is { } type)
        {
            transactions = transactions.Where(t => t.Type == type);
        }

        if (query.From is { } from)
        {
            transactions = transactions.Where(t => t.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            transactions = transactions.Where(t => t.OccurredAt <= to);
        }

        var totalCount = await transactions.CountAsync(cancellationToken);

        var items = await transactions
            .OrderByDescending(t => t.Sequence)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TransactionResult(
                t.Id,
                t.AccountId,
                t.Sequence,
                t.Type,
                t.Amount,
                t.BalanceAfter,
                t.Description,
                t.OccurredAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<TransactionResult>(items, query.Page, query.PageSize, totalCount);
    }
}
