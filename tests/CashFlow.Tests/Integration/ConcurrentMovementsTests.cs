using System.Net;
using CashFlow.Domain.Accounts;

namespace CashFlow.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ConcurrentMovementsTests(CashFlowApiFactory factory) : IClassFixture<CashFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ParallelWithdrawals_NeverDriveTheBalanceNegative()
    {
        // Arrange
        const decimal initialBalance = 100m;
        const decimal withdrawalAmount = 10m;
        const int attempts = 25;

        var accountId = await _client.CreateAccountAsync();
        await _client.DepositAsync(accountId, initialBalance);

        // Act
        var responses = await Task.WhenAll(
            Enumerable.Range(0, attempts).Select(_ => _client.WithdrawAsync(accountId, withdrawalAmount)));
        var finalBalance = await _client.GetBalanceAsync(accountId);

        // Assert
        var outcome = Summarize(responses);

        Assert.Equal(attempts, outcome.Created + outcome.Rejected + outcome.Conflicted);
        Assert.Equal(0, outcome.ServerErrors);
        Assert.True(finalBalance >= 0m, $"Balance went negative: {finalBalance}");
        Assert.Equal(initialBalance - (outcome.Created * withdrawalAmount), finalBalance);
        Assert.True(outcome.Created <= initialBalance / withdrawalAmount);
    }

    [Fact]
    public async Task MixedParallelDepositsAndWithdrawals_KeepTheLedgerConsistent()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();
        await _client.DepositAsync(accountId, 500m);

        var operations = Enumerable.Range(1, 30)
            .Select(i => i % 3 == 0
                ? _client.DepositAsync(accountId, 25m)
                : _client.WithdrawAsync(accountId, 40m))
            .ToList();

        // Act
        var responses = await Task.WhenAll(operations);
        var finalBalance = await _client.GetBalanceAsync(accountId);
        var statement = await _client.GetHistoryAsync(accountId, "pageSize=100");

        // Assert
        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        Assert.True(finalBalance >= 0m);

        var credits = statement.Items.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        var debits = statement.Items.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);

        Assert.Equal(credits - debits, finalBalance);
        Assert.Equal(statement.TotalCount, statement.Items.Count);
        Assert.Equal(finalBalance, statement.Items[0].BalanceAfter);
        Assert.Equal(Enumerable.Range(1, statement.TotalCount).Reverse(), statement.Items.Select(t => t.Sequence));
    }

    private static Outcome Summarize(IEnumerable<HttpResponseMessage> responses)
    {
        var byStatus = responses.ToLookup(r => r.StatusCode);

        return new Outcome(
            Created: byStatus[HttpStatusCode.Created].Count(),
            Rejected: byStatus[HttpStatusCode.UnprocessableEntity].Count(),
            Conflicted: byStatus[HttpStatusCode.Conflict].Count(),
            ServerErrors: responses.Count(r => (int)r.StatusCode >= 500));
    }

    private sealed record Outcome(int Created, int Rejected, int Conflicted, int ServerErrors);
}
