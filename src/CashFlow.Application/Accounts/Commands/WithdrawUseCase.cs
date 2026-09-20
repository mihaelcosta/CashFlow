using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;
using CashFlow.Domain.Accounts;
using CashFlow.Domain.Common;

namespace CashFlow.Application.Accounts.Commands;

public sealed class WithdrawUseCase(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IUseCase<WithdrawCommand, TransactionResult>
{
    public async Task<TransactionResult> ExecuteAsync(WithdrawCommand request, CancellationToken cancellationToken = default)
    {
        var account = await accounts.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.AccountId);

        var transaction = account.Withdraw(request.Amount, request.Description, clock.GetUtcNow());

        accounts.Add(transaction);
        await unitOfWork.CommitAsync(cancellationToken);

        return TransactionResult.From(transaction);
    }
}
