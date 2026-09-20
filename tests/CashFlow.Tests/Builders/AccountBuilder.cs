using CashFlow.Domain.Accounts;

namespace CashFlow.Tests.Builders;

internal sealed class AccountBuilder
{
    public static readonly DateTimeOffset DefaultDate = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private Guid? _id;
    private string _name = "Acme Ltda";
    private decimal _balance;
    private DateTimeOffset _createdAt = DefaultDate;

    public AccountBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public AccountBuilder Named(string name)
    {
        _name = name;
        return this;
    }

    public AccountBuilder WithBalance(decimal balance)
    {
        _balance = balance;
        return this;
    }

    public AccountBuilder CreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public Account Build()
    {
        var account = _id is { } id
            ? new Account(id, _name, _createdAt)
            : new Account(_name, _createdAt);

        if (_balance > 0)
        {
            account.Deposit(_balance, "Opening balance", _createdAt);
        }

        return account;
    }

    public static Account Empty() => new AccountBuilder().Build();

    public static Account WithFunds(decimal balance) => new AccountBuilder().WithBalance(balance).Build();
}
