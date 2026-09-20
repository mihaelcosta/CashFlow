using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;
using CashFlow.Domain.Accounts;

namespace CashFlow.Application.Accounts.Queries;

public sealed class GetBalanceUseCase(IAccountRepository accounts) : IUseCase<GetBalanceQuery, BalanceResult>
{
    public async Task<BalanceResult> ExecuteAsync(GetBalanceQuery request, CancellationToken cancellationToken = default)
    {
        var account = await accounts.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.AccountId);

        return new BalanceResult(account.Id, account.Balance);
    }
}
