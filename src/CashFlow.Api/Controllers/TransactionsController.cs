using CashFlow.Api.Contracts;
using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Commands;
using CashFlow.Application.Accounts.Queries;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Controllers;

[ApiController]
[Route("api/accounts/{accountId:guid}")]
[Produces("application/json")]
public sealed class TransactionsController(
    IUseCase<DepositCommand, TransactionResult> deposit,
    IUseCase<WithdrawCommand, TransactionResult> withdraw,
    IUseCase<GetTransactionsQuery, PagedResult<TransactionResult>> getTransactions) : ControllerBase
{
    [HttpPost("deposits")]
    [ProducesResponseType<TransactionResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResult>> Deposit(Guid accountId, MovementRequest request, CancellationToken cancellationToken)
    {
        var transaction = await deposit.ExecuteAsync(
            new DepositCommand(accountId, request.Amount, request.Description),
            cancellationToken);

        return Created(transaction);
    }

    [HttpPost("withdrawals")]
    [ProducesResponseType<TransactionResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResult>> Withdraw(Guid accountId, MovementRequest request, CancellationToken cancellationToken)
    {
        var transaction = await withdraw.ExecuteAsync(
            new WithdrawCommand(accountId, request.Amount, request.Description),
            cancellationToken);

        return Created(transaction);
    }

    [HttpGet("transactions")]
    [ProducesResponseType<PagedResult<TransactionResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TransactionResult>>> GetHistory(
        Guid accountId,
        [FromQuery] TransactionHistoryRequest request,
        CancellationToken cancellationToken)
        => await getTransactions.ExecuteAsync(
            new GetTransactionsQuery(accountId, request.Type, request.From, request.To, request.Page, request.PageSize),
            cancellationToken);

    private CreatedResult Created(TransactionResult transaction)
        => Created(
            Url.Action(nameof(GetHistory), new { accountId = transaction.AccountId }),
            transaction);
}
