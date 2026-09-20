using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Results;
using CashFlow.Domain.Accounts;
using CashFlow.Domain.Common;

namespace CashFlow.Application.Accounts.Commands;

public sealed class CreateAccountUseCase(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IUseCase<CreateAccountCommand, AccountResult>
{
    public async Task<AccountResult> ExecuteAsync(CreateAccountCommand request, CancellationToken cancellationToken = default)
    {
        var account = new Account(request.Name, clock.GetUtcNow());

        accounts.Add(account);
        await unitOfWork.CommitAsync(cancellationToken);

        return AccountResult.From(account);
    }
}
