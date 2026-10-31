using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Application.Contracts;

namespace FinTrack.Tests.Api;

/// <summary>
/// End-to-end contract tests through the real ASP.NET Core pipeline (routing,
/// model binding, auth, ProblemDetails mapping) with an InMemory database.
/// Demo users are provisioned by appsettings (see Auth:Development:Users).
/// </summary>
public sealed class ApiContractTests(FinTrackApiFactory factory) : IClassFixture<FinTrackApiFactory>
{
    private const string CheckingNumberAmira = "FB-1029384756";
    private const string SavingsNumberAmira = "FB-5647382910";
    private const string CheckingNumberTarek = "FB-7483920165";

    private Task<(HttpClient Client, string Token)>? _staffLogin;

    // ── authentication & authorization ───────────────────────────────────────

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_demo_user_is_rejected()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/token", new { userName = "intruder" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_list_another_customers_accounts()
    {
        var amira = (await LoginAsync("amira")).Client;
        var tarekCustomerId = await CustomerIdForAccountAsync(CheckingNumberTarek);

        var response = await amira.GetAsync($"/api/accounts?customerId={tarekCustomerId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── happy paths ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Staff_lists_seeded_accounts()
    {
        var staff = (await LoginAsync("teller")).Client;

        var response = await staff.GetAsync("/api/accounts");
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountSummaryDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, accounts!.Count);
    }

    [Fact]
    public async Task Customer_posts_transfer_and_both_ledgers_update()
    {
        var amira = (await LoginAsync("amira")).Client;
        var accounts = await amira.GetFromJsonAsync<List<AccountSummaryDto>>(
            $"/api/accounts?customerId={await CustomerIdForAccountAsync(CheckingNumberAmira)}");
        var from = accounts!.Single(a => a.Number == CheckingNumberAmira);
        var toId = await AccountIdForNumberAsync(CheckingNumberTarek);

        var response = await amira.PostAsJsonAsync("/api/transfers",
            new TransferRequest(from.Id, toId, 100.50m, "Contract test"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransferResultDto>();
        Assert.StartsWith("TRF-", result!.TransferReference);
        Assert.Equal(from.Balance - 100.50m, result.FromBalanceAfter);

        // Source ledger shows the outgoing entry with the shared reference.
        var staff = (await StaffAsync()).Client;
        var ledger = await staff.GetFromJsonAsync<PagedResult<LedgerEntryDto>>(
            $"/api/accounts/{from.Id}/transactions?page=1&pageSize=5");
        Assert.Contains(ledger!.Items, t => t.Reference == result.TransferReference && t.Amount == -100.50m);
    }

    [Fact]
    public async Task Insufficient_funds_returns_409_with_stable_code()
    {
        var amira = (await LoginAsync("amira")).Client;
        var accounts = await amira.GetFromJsonAsync<List<AccountSummaryDto>>(
            $"/api/accounts?customerId={await CustomerIdForAccountAsync(CheckingNumberAmira)}");
        var from = accounts!.Single(a => a.Number == CheckingNumberAmira);
        var toId = await AccountIdForNumberAsync(CheckingNumberTarek);

        // Under the 25,000 per-transaction cap but above the seeded balance (~4,250).
        var response = await amira.PostAsJsonAsync("/api/transfers",
            new TransferRequest(from.Id, toId, 5_000m, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INSUFFICIENT_FUNDS", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Customer_cannot_transfer_using_foreign_source_account()
    {
        var amira = (await LoginAsync("amira")).Client;
        var tarekCheckingId = await AccountIdForNumberAsync(CheckingNumberTarek);

        var response = await amira.PostAsJsonAsync("/api/transfers",
            new TransferRequest(tarekCheckingId, Guid.NewGuid(), 1m, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("NOT_ACCOUNT_OWNER", await ProblemCodeAsync(response));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, string Token)> LoginAsync(string userName)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/token", new { userName });
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    private Task<(HttpClient Client, string Token)> StaffAsync() =>
        _staffLogin ??= LoginAsync("admin");

    private async Task<string> CustomerIdForAccountAsync(string accountNumber)
    {
        var staff = (await StaffAsync()).Client;
        var accounts = await staff.GetFromJsonAsync<List<AccountSummaryDto>>("/api/accounts");
        return accounts!.Single(a => a.Number == accountNumber).CustomerId.ToString();
    }

    private async Task<Guid> AccountIdForNumberAsync(string accountNumber)
    {
        var staff = (await StaffAsync()).Client;
        var accounts = await staff.GetFromJsonAsync<List<AccountSummaryDto>>("/api/accounts");
        return accounts!.Single(a => a.Number == accountNumber).Id;
    }

    private static async Task<string> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString()!;
    }
}
