using System.ComponentModel.DataAnnotations;

namespace CashFlow.Api.Contracts;

public sealed record MovementRequest(
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")] decimal Amount,
    [StringLength(200)] string? Description);
