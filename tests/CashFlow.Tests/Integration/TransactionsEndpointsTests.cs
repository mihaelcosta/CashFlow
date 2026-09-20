using System.Net;
using System.Text;
using System.Text.Json;
using CashFlow.Application.Accounts.Results;
using CashFlow.Domain.Accounts;
using CashFlow.Domain.Accounts.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class TransactionsEndpointsTests(CashFlowApiFactory factory) : IClassFixture<CashFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Deposit_ReturnsCreated_WithTransactionBody_AndLocationToHistory()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();

        // Act
        var response = await _client.DepositAsync(accountId, 500m, "Initial capital");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"/api/accounts/{accountId}/transactions", response.Headers.Location?.ToString());

        var transaction = await response.ReadAsAsync<TransactionResult>();
        Assert.NotNull(transaction);
        Assert.Equal(TransactionType.Credit, transaction.Type);
        Assert.Equal(500m, transaction.Amount);
        Assert.Equal(500m, transaction.BalanceAfter);
        Assert.Equal("Initial capital", transaction.Description);
    }

    [Fact]
    public async Task Transaction_SerializesTypeAsString_NotAsNumber()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();

        // Act
        var response = await _client.DepositAsync(accountId, 1m);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        var type = json.RootElement.GetProperty("type");
        Assert.Equal(JsonValueKind.String, type.ValueKind);
        Assert.Equal("Credit", type.GetString());
    }

    [Fact]
    public async Task Withdraw_BeyondBalance_ReturnsUnprocessableEntity_AndKeepsBalance()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();
        await _client.DepositAsync(accountId, 50m);

        // Act
        var response = await _client.WithdrawAsync(accountId, 50.01m);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.ReadAsAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Business rule violated", problem.Title);
        Assert.Equal(nameof(InsufficientFundsException), problem.Extensions["errorType"]?.ToString());
        Assert.Contains("Balance: 50", problem.Detail);
        Assert.Equal(50m, await _client.GetBalanceAsync(accountId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Movement_WithNonPositiveAmount_IsRejectedAtTheContractLevel(decimal amount)
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();

        // Act
        var deposit = await _client.DepositAsync(accountId, amount);
        var withdrawal = await _client.WithdrawAsync(accountId, amount);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, deposit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withdrawal.StatusCode);

        var problem = await deposit.ReadAsAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Amount", problem.Errors.Keys);
    }

    [Fact]
    public async Task Movement_WithDescriptionOver200Chars_ReturnsBadRequest()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();

        // Act
        var response = await _client.DepositAsync(accountId, 10m, new string('d', 201));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Movement_WithMalformedJson_ReturnsBadRequest()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();
        using var content = new StringContent("{ \"amount\": \"ten\" }", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync($"/api/accounts/{accountId}/deposits", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Movement_ForUnknownAccount_ReturnsNotFound()
    {
        // Act
        var response = await _client.DepositAsync(Guid.NewGuid(), 10m);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task History_FiltersByType_AndPaginates()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();
        await _client.DepositAsync(accountId, 100m);
        await _client.DepositAsync(accountId, 100m);
        await _client.DepositAsync(accountId, 100m);
        await _client.WithdrawAsync(accountId, 10m);
        await _client.WithdrawAsync(accountId, 10m);

        // Act
        var credits = await _client.GetHistoryAsync(accountId, "type=Credit&page=1&pageSize=2");
        var debits = await _client.GetHistoryAsync(accountId, "type=Debit");

        // Assert
        Assert.Equal(3, credits.TotalCount);
        Assert.Equal(2, credits.TotalPages);
        Assert.Equal(2, credits.Items.Count);
        Assert.All(credits.Items, t => Assert.Equal(TransactionType.Credit, t.Type));

        Assert.Equal(2, debits.TotalCount);
        Assert.All(debits.Items, t => Assert.Equal(TransactionType.Debit, t.Type));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("type=Transfer")]
    public async Task History_WithInvalidQuery_ReturnsBadRequest(string queryString)
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();

        // Act
        var response = await _client.GetAsync($"/api/accounts/{accountId}/transactions?{queryString}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task History_ForUnknownAccount_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/accounts/{Guid.NewGuid()}/transactions");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
