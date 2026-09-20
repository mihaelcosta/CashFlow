using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;

namespace CashFlow.Tests.Integration;

internal static class ApiClientExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<Guid> CreateAccountAsync(this HttpClient client, string name = "Test Company")
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new { name }, Json);
        response.EnsureSuccessStatusCode();

        var account = await response.Content.ReadFromJsonAsync<AccountResult>(Json);
        return account!.Id;
    }

    public static Task<HttpResponseMessage> DepositAsync(this HttpClient client, Guid accountId, decimal amount, string? description = null)
        => client.PostAsJsonAsync($"/api/accounts/{accountId}/deposits", new { amount, description }, Json);

    public static Task<HttpResponseMessage> WithdrawAsync(this HttpClient client, Guid accountId, decimal amount, string? description = null)
        => client.PostAsJsonAsync($"/api/accounts/{accountId}/withdrawals", new { amount, description }, Json);

    public static async Task<decimal> GetBalanceAsync(this HttpClient client, Guid accountId)
    {
        var balance = await client.GetFromJsonAsync<BalanceResult>($"/api/accounts/{accountId}/balance", Json);
        return balance!.Balance;
    }

    public static async Task<PagedResult<TransactionResult>> GetHistoryAsync(this HttpClient client, Guid accountId, string? queryString = null)
    {
        var url = $"/api/accounts/{accountId}/transactions";
        if (!string.IsNullOrEmpty(queryString))
        {
            url += "?" + queryString;
        }

        var page = await client.GetFromJsonAsync<PagedResult<TransactionResult>>(url, Json);
        return page!;
    }

    public static Task<T?> ReadAsAsync<T>(this HttpResponseMessage response)
        => response.Content.ReadFromJsonAsync<T>(Json);
}
