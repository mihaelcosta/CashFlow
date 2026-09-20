using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;
using CashFlow.Domain.Accounts;

namespace CashFlow.Application.Accounts.Queries;

public sealed class GetTransactionsUseCase(
    IAccountRepository accounts,
    ITransactionHistoryReader history) : IUseCase<GetTransactionsQuery, PagedResult<TransactionResult>>
{
    public async Task<PagedResult<TransactionResult>> ExecuteAsync(GetTransactionsQuery request, CancellationToken cancellationToken = default)
    {
        _ = await accounts.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.AccountId);

        return await history.ReadAsync(request, cancellationToken);
    }
}
