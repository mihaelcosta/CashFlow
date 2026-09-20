using System.ComponentModel.DataAnnotations;

namespace CashFlow.Api.Contracts;

public sealed record CreateAccountRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name);
