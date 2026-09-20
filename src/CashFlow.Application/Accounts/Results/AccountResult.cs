using CashFlow.Domain.Accounts;

namespace CashFlow.Application.Accounts.Results;

public sealed record AccountResult(Guid Id, string Name, decimal Balance, DateTimeOffset CreatedAt)
{
    public static AccountResult From(Account account)
        => new(account.Id, account.Name, account.Balance, account.CreatedAt);
}
