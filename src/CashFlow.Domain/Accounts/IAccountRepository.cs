namespace CashFlow.Domain.Accounts;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Account account);
    void Add(Transaction transaction);
}
