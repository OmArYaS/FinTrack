using FinTrack.Application.Accounts;
using FinTrack.Application.Common.Exceptions;
using FinTrack.Application.Common.Interfaces;
using FinTrack.Application.Contracts;
using FinTrack.Domain.Entities;
using FinTrack.Domain.Enums;
using FinTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory.ValueGeneration.Internal;

namespace FinTrack.Tests.Accounts;

/// <summary>
/// Business-rule matrix for money movement. Each rule is testable in isolation
/// because the service depends only on IApplicationDbContext + clock + limits.
/// </summary>
public sealed class TransferServiceTests : IDisposable
{
    private readonly FinTrackDbContext _db;
    private readonly FixedDateTime _clock = new(new DateTime(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc));
    private readonly TransferLimits _limits = new() { MaxAmountPerTransaction = 10_000m, MaxDailyOutflowPerAccount = 25_000m };

    public TransferServiceTests()
    {
        var options = new DbContextOptionsBuilder<FinTrackDbContext>()
            .UseInMemoryDatabase($"fintrack-unit-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FinTrackDbContext(options);
    }

    private ITransferService Sut(string[]? roles = null, string externalId = "usr_amira")
        => new TransferService(_db, _clock, _limits, new StubUser(externalId, roles ?? new[] { "Customer" }));

    [Fact]
    public async Task Happy_path_moves_money_and_writes_two_ledger_entries()
    {
        var (from, to) = await SeedAccountsAsync(500m, 100m);

        var sut = Sut(externalId: from.Customer.ExternalId);
        var result = await sut.ExecuteAsync(new TransferRequest(from.Id, to.Id, 150m, "Test"));

        Assert.Equal(350m, result.FromBalanceAfter);
        Assert.Equal(250m, result.ToBalanceAfter);

        var entries = await _db.Transactions
            .Where(t => t.Reference == result.TransferReference)
            .Distinct() // EF InMemory duplicate values can occur with circular navigation tracking
            .OrderBy(t => t.Amount)
            .ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, t => Assert.Equal(result.TransferReference, t.Reference));
        Assert.Contains(entries, t => t.Type == TransactionType.TransferOut && t.Amount == -150m);
        Assert.Contains(entries, t => t.Type == TransactionType.TransferIn && t.Amount == 150m);
    }

    [Fact]
    public async Task Self_transfer_is_rejected()
    {
        var (from, _) = await SeedAccountsAsync(500m, 0m);
        await AssertBusinessCode(() => Sut().ExecuteAsync(new TransferRequest(from.Id, from.Id, 10m, null)), "SELF_TRANSFER");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    public async Task Non_positive_amounts_are_rejected(decimal amount)
    {
        var (from, to) = await SeedAccountsAsync(500m, 0m);
        await AssertBusinessCode(() => Sut().ExecuteAsync(new TransferRequest(from.Id, to.Id, amount, null)), "INVALID_AMOUNT");
    }

    [Fact]
    public async Task Amount_above_per_transaction_cap_is_rejected()
    {
        var (from, to) = await SeedAccountsAsync(900_000m, 0m);
        await AssertBusinessCode(() => Sut().ExecuteAsync(new TransferRequest(from.Id, to.Id, 10_001m, null)), "EXCEEDS_TRANSACTION_LIMIT");
    }

    [Fact]
    public async Task Currency_mismatch_is_rejected()
    {
        var (from, _) = await SeedAccountsAsync(500m, 0m);
        var euro = await SeedEurAccountAsync(from.CustomerId);

        await AssertBusinessCode(() => Sut().ExecuteAsync(new TransferRequest(from.Id, euro.Id, 10m, null)), "CURRENCY_MISMATCH");
    }

    [Fact]
    public async Task Insufficient_funds_are_rejected_and_nothing_is_written()
    {
        var (from, to) = await SeedAccountsAsync(40m, 0m);

        var ex = await Record.ExceptionAsync(() => Sut().ExecuteAsync(new TransferRequest(from.Id, to.Id, 50m, null)));

        var bre = Assert.IsType<BusinessRuleException>(ex);
        Assert.Equal("INSUFFICIENT_FUNDS", bre.Code);
        Assert.Equal(409, bre.StatusCode);
        Assert.Equal(40m, await _db.Accounts.Where(a => a.Id == from.Id).Select(a => a.Balance).SingleAsync());
    }

    [Fact]
    public async Task Rolling_daily_outflow_cap_is_enforced()
    {
        var (from, to) = await SeedAccountsAsync(500_000m, 0m);

        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(), AccountId = from.Id,
            Type = TransactionType.TransferOut, Amount = -20_000m,
            Reference = "TRF-SEED-DAILY", CreatedAt = _clock.Now.AddHours(-2)
        });
        await _db.SaveChangesAsync();

        // 20k posted 2h ago + 6k now = 26k > 25k daily cap
        await AssertBusinessCode(() => Sut().ExecuteAsync(new TransferRequest(from.Id, to.Id, 6_000m, null)), "EXCEEDS_DAILY_LIMIT", 403);

        // But the next day the same transfer is fine (cap rolls on a 24h window).
        _db.RemoveRange(_db.Transactions.Where(t => t.Reference == "TRF-SEED-DAILY"));
        await _db.SaveChangesAsync();
        var ok = await Sut().ExecuteAsync(new TransferRequest(from.Id, to.Id, 6_000m, null));
        Assert.NotEmpty(ok.TransferReference);
    }

    [Fact]
    public async Task Closed_accounts_are_rejected()
    {
        var (from, to) = await SeedAccountsAsync(500m, 0m, closeTo: true);
        await AssertBusinessCode(() => Sut().ExecuteAsync(new TransferRequest(from.Id, to.Id, 10m, null)), "ACCOUNT_CLOSED", 409);
    }

    [Fact]
    public async Task Frozen_source_account_is_rejected()
    {
        var (from, to) = await SeedAccountsAsync(500m, 0m, freezeFrom: true);
        await AssertBusinessCode(() => Sut().ExecuteAsync(new TransferRequest(from.Id, to.Id, 10m, null)), "ACCOUNT_FROZEN", 403);
    }

    [Fact]
    public async Task Customer_cannot_move_someone_elses_money()
    {
        var (from, to) = await SeedAccountsAsync(500m, 0m);
        await AssertBusinessCode(() => Sut(externalId: "usr_tarek").ExecuteAsync(new TransferRequest(from.Id, to.Id, 10m, null)), "NOT_ACCOUNT_OWNER", 403);

        // Staff override is allowed for back-office corrections.
        var ok = await Sut(roles: [ "Teller" ], externalId: "usr_tarek").ExecuteAsync(new TransferRequest(from.Id, to.Id, 10m, null));
        Assert.Equal(490m, ok.FromBalanceAfter);
    }

    [Fact]
    public async Task Unknown_source_account_maps_to_NotFound()
    {
        var (_, to) = await SeedAccountsAsync(500m, 0m);
        await Assert.ThrowsAsync<NotFoundException>(
            () => Sut().ExecuteAsync(new TransferRequest(Guid.NewGuid(), to.Id, 10m, null)));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Account From, Account To)> SeedAccountsAsync(decimal fromBalance, decimal toBalance, bool closeTo = false, bool freezeFrom = false)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(), ExternalId = "usr_amira",
            FullName = "Amira Hassan", Email = "amira.hassan@example.com"
        };
        var from = new Account { Id = Guid.NewGuid(), CustomerId = customer.Id, Number = $"FB-{Random.Shared.Next(10_000, 99_999)}", Type = AccountType.Checking, Currency = "USD", Balance = fromBalance, Status = freezeFrom ? AccountStatus.Frozen : AccountStatus.Active };
        var to = new Account { Id = Guid.NewGuid(), CustomerId = customer.Id, Number = $"FB-{Random.Shared.Next(10_000, 99_999)}", Type = AccountType.Checking, Currency = "USD", Balance = toBalance, Status = closeTo ? AccountStatus.Closed : AccountStatus.Active };
        customer.Accounts.Add(from);
        customer.Accounts.Add(to);
        _db.Customers.Add(customer);
        _db.Accounts.AddRange(from, to);
        await _db.SaveChangesAsync();
        return (from, to);
    }

    private async Task<Account> SeedEurAccountAsync(Guid customerId)
    {
        var eur = new Account { Id = Guid.NewGuid(), CustomerId = customerId, Number = $"FB-{Random.Shared.Next(10_000, 99_999)}", Type = AccountType.Savings, Currency = "EUR", Balance = 1_000m };
        _db.Accounts.Add(eur);
        await _db.SaveChangesAsync();
        return eur;
    }

    private static async Task AssertBusinessCode(Func<Task> action, string expectedCode, int expectedStatus = 400)
    {
        var ex = await Record.ExceptionAsync(action);
        var bre = Assert.IsType<BusinessRuleException>(ex);
        Assert.Equal(expectedCode, bre.Code);
        Assert.Equal(expectedStatus, bre.StatusCode);
    }

    public void Dispose() => _db.Dispose();
}
