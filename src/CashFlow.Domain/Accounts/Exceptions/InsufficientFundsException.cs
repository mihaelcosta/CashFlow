using System.Globalization;
using CashFlow.Domain.Common;

namespace CashFlow.Domain.Accounts.Exceptions;

public sealed class InsufficientFundsException(decimal balance, decimal requested)
    : DomainException(string.Create(CultureInfo.InvariantCulture, $"Insufficient funds. Balance: {balance}, requested: {requested}."))
{
    public decimal Balance { get; } = balance;
    public decimal Requested { get; } = requested;
}
