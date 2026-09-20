namespace CashFlow.Application.Accounts.Results;

public sealed record BalanceResult(Guid AccountId, decimal Balance);
