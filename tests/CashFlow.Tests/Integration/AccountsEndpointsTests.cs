using System.Net;
using System.Net.Http.Json;
using CashFlow.Application.Accounts.Results;
using CashFlow.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class AccountsEndpointsTests(CashFlowApiFactory factory) : IClassFixture<CashFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateAccount_ReturnsCreated_WithLocationHeader_AndZeroBalance()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/accounts", new { name = "Acme Ltda" }, ApiClientExtensions.Json);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var account = await response.ReadAsAsync<AccountResult>();
        Assert.NotNull(account);
        Assert.Equal("Acme Ltda", account.Name);
        Assert.Equal(0m, account.Balance);
        Assert.EndsWith($"/api/accounts/{account.Id}/balance", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAccount_WithBlankName_ReturnsValidationProblem(string name)
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/accounts", new { name }, ApiClientExtensions.Json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Name", problem.Errors.Keys);
    }

    [Fact]
    public async Task CreateAccount_WithNameOver100Chars_ReturnsValidationProblem()
    {
        // Arrange
        var name = new string('x', 101);

        // Act
        var response = await _client.PostAsJsonAsync("/api/accounts", new { name }, ApiClientExtensions.Json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_ForSeededDefaultAccount_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync($"/api/accounts/{DefaultAccount.Id}/balance");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var balance = await response.ReadAsAsync<BalanceResult>();
        Assert.NotNull(balance);
        Assert.Equal(DefaultAccount.Id, balance.AccountId);
    }

    [Fact]
    public async Task GetBalance_ForUnknownAccount_ReturnsNotFoundProblem()
    {
        // Act
        var response = await _client.GetAsync($"/api/accounts/{Guid.NewGuid()}/balance");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.ReadAsAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("NotFoundException", problem.Extensions["errorType"]?.ToString());
    }

    [Fact]
    public async Task GetBalance_WithMalformedId_ReturnsNotFound_FromRouteConstraint()
    {
        // Act
        var response = await _client.GetAsync("/api/accounts/not-a-guid/balance");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
