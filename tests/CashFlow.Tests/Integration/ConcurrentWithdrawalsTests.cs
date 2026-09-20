using System.Net;

namespace CashFlow.Tests.Integration;

public sealed class ConcurrentWithdrawalsTests(CashFlowApiFactory factory) : IClassFixture<CashFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ParallelWithdrawals_NeverDriveBalanceNegative()
    {
        const decimal initialBalance = 100m;
        const decimal withdrawalAmount = 10m;
        const int attempts = 25;

        var accountId = await _client.CreateAccountAsync();
        await _client.DepositAsync(accountId, initialBalance);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, attempts).Select(_ => _client.WithdrawAsync(accountId, withdrawalAmount)));

        var statuses = responses.Select(r => r.StatusCode).ToLookup(s => s);
        var succeeded = statuses[HttpStatusCode.Created].Count();
        var rejected = statuses[HttpStatusCode.UnprocessableEntity].Count();
        var conflicted = statuses[HttpStatusCode.Conflict].Count();

        Assert.Equal(attempts, succeeded + rejected + conflicted);
        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);

        var finalBalance = await _client.GetBalanceAsync(accountId);

        Assert.True(finalBalance >= 0m, $"Balance went negative: {finalBalance}");
        Assert.Equal(initialBalance - (succeeded * withdrawalAmount), finalBalance);
        Assert.True(succeeded <= initialBalance / withdrawalAmount);
    }
}
