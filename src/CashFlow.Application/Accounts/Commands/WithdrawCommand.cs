namespace CashFlow.Application.Accounts.Commands;

public sealed record WithdrawCommand(Guid AccountId, decimal Amount, string? Description);
