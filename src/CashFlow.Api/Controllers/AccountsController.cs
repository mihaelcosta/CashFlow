using CashFlow.Api.Contracts;
using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Commands;
using CashFlow.Application.Accounts.Queries;
using CashFlow.Application.Accounts.Results;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Produces("application/json")]
public sealed class AccountsController(
    IUseCase<CreateAccountCommand, AccountResult> createAccount,
    IUseCase<GetBalanceQuery, BalanceResult> getBalance) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AccountResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountResult>> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await createAccount.ExecuteAsync(new CreateAccountCommand(request.Name), cancellationToken);

        return CreatedAtAction(nameof(GetBalance), new { id = account.Id }, account);
    }

    [HttpGet("{id:guid}/balance")]
    [ProducesResponseType<BalanceResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalanceResult>> GetBalance(Guid id, CancellationToken cancellationToken)
        => await getBalance.ExecuteAsync(new GetBalanceQuery(id), cancellationToken);
}
