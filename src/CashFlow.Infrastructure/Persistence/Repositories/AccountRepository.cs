using CashFlow.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(CashFlowDbContext context) : IAccountRepository
{
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Add(Account account)
        => context.Accounts.Add(account);

    public void Add(Transaction transaction)
        => context.Transactions.Add(transaction);
}
