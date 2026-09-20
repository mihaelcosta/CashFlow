using System.Net;
using CashFlow.Application.Accounts.Results;
using CashFlow.Domain.Accounts;

namespace CashFlow.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class MonthlyStatementScenarioTests(CashFlowApiFactory factory) : IClassFixture<CashFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ASmallBusinessMonth_EndsWithAConsistentStatement()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync("Padaria do Bairro");

        // Act
        var openingCapital = await _client.DepositAsync(accountId, 20_000m, "Opening capital");
        var rent = await _client.WithdrawAsync(accountId, 4_500m, "Rent - September");
        var week1 = await _client.DepositAsync(accountId, 6_250.40m, "Sales - week 1");
        var supplier = await _client.WithdrawAsync(accountId, 8_900.15m, "Flour supplier");
        var week2 = await _client.DepositAsync(accountId, 5_800m, "Sales - week 2");
        var payroll = await _client.WithdrawAsync(accountId, 12_000m, "Payroll");
        var expansionAttempt = await _client.WithdrawAsync(accountId, 10_000m, "New oven");
        var week3 = await _client.DepositAsync(accountId, 7_100.75m, "Sales - week 3");

        var balance = await _client.GetBalanceAsync(accountId);
        var statement = await _client.GetHistoryAsync(accountId, "pageSize=50");
        var onlyDebits = await _client.GetHistoryAsync(accountId, "type=Debit&pageSize=50");

        // Assert
        Assert.All(
            new[] { openingCapital, rent, week1, supplier, week2, payroll, week3 },
            r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expansionAttempt.StatusCode);

        const decimal expectedBalance = 20_000m - 4_500m + 6_250.40m - 8_900.15m + 5_800m - 12_000m + 7_100.75m;
        Assert.Equal(expectedBalance, balance);

        Assert.Equal(7, statement.TotalCount);
        Assert.DoesNotContain(statement.Items, t => t.Description == "New oven");
        Assert.Equal(expectedBalance, statement.Items[0].BalanceAfter);

        var chronological = statement.Items.Reverse().ToList();
        var runningTotal = 0m;
        foreach (var transaction in chronological)
        {
            runningTotal += transaction.Type == TransactionType.Credit ? transaction.Amount : -transaction.Amount;
            Assert.Equal(runningTotal, transaction.BalanceAfter);
        }

        Assert.Equal(3, onlyDebits.TotalCount);
        Assert.Equal(4_500m + 8_900.15m + 12_000m, onlyDebits.Items.Sum(t => t.Amount));
    }

    [Fact]
    public async Task Statement_CanBeFilteredByPeriod()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();
        var clock = factory.Clock;

        await _client.DepositAsync(accountId, 100m, "August");
        clock.Advance(TimeSpan.FromDays(31));
        var septemberStart = clock.GetUtcNow();
        await _client.DepositAsync(accountId, 200m, "September");
        clock.Advance(TimeSpan.FromDays(30));
        var octoberStart = clock.GetUtcNow();
        await _client.DepositAsync(accountId, 300m, "October");

        // Act
        var september = await _client.GetHistoryAsync(accountId, $"from={Iso(septemberStart)}&to={Iso(octoberStart.AddTicks(-1))}");
        var untilAugust = await _client.GetHistoryAsync(accountId, $"to={Iso(septemberStart.AddTicks(-1))}");
        var fromOctober = await _client.GetHistoryAsync(accountId, $"from={Iso(octoberStart)}");

        // Assert
        Assert.Equal(["September"], september.Items.Select(t => t.Description));
        Assert.Equal(["August"], untilAugust.Items.Select(t => t.Description));
        Assert.Equal(["October"], fromOctober.Items.Select(t => t.Description));
    }

    [Fact]
    public async Task Movements_AreStampedWithTheApplicationClock()
    {
        // Arrange
        var accountId = await _client.CreateAccountAsync();
        var expected = factory.Clock.GetUtcNow();

        // Act
        var response = await _client.DepositAsync(accountId, 1m);
        var transaction = await response.ReadAsAsync<TransactionResult>();

        // Assert
        Assert.NotNull(transaction);
        Assert.Equal(expected, transaction.OccurredAt);
    }

    private static string Iso(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));
}
