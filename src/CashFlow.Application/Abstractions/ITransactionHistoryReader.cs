using CashFlow.Application.Accounts.Queries;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;

namespace CashFlow.Application.Abstractions;

public interface ITransactionHistoryReader
{
    Task<PagedResult<TransactionResult>> ReadAsync(GetTransactionsQuery query, CancellationToken cancellationToken = default);
}
