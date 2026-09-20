namespace CashFlow.Application.Accounts.Commands;

public sealed record DepositCommand(Guid AccountId, decimal Amount, string? Description);
