using System.Globalization;
using CashFlow.Domain.Common;

namespace CashFlow.Domain.Accounts.Exceptions;

public sealed class InvalidAmountException(decimal amount)
    : DomainException(string.Create(CultureInfo.InvariantCulture, $"Amount must be greater than zero. Received: {amount}."))
{
    public decimal Amount { get; } = amount;
}
