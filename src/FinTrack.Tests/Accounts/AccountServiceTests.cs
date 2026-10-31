using FinTrack.Application.Accounts;
using FinTrack.Application.Common.Exceptions;
using FinTrack.Application.Contracts;
using FinTrack.Domain.Entities;
using FinTrack.Domain.Enums;
using FinTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory.ValueGeneration.Internal;

namespace FinTrack.Tests.Accounts;

/// <summary>Account lifecycle rules: open (type/currency validation), close (zero-balance first), read paths.</summary>
public sealed class AccountServiceTests
{
    private readonly FinTrackDbContext _db;
    private readonly FixedDateTime _clock = new(new DateTime(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc));
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        var options = new DbContextOptionsBuilder<FinTrackDbContext>()
            .UseInMemoryDatabase($"fintrack-unit-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FinTrackDbContext(options);
        _sut = new AccountService(_db, _clock);
    }

    [Theory]
    [InlineData("checking")]
    [InlineData("SAVINGS")]
    [InlineData("FixedDeposit")]
    public async Task Open_is_case_insensitive_on_type_and_normalizes_currency(string type)
    {
        await SeedCustomerAsync("usr_a", "a@example.com");

        var account = await _sut.OpenAsync(new OpenAccountRequest("usr_a", type, "usd"));

        Assert.Equal("USD", account.Currency);
        Assert.StartsWith("FB-", account.Number);
        Assert.Equal("Active", account.Status);
    }

    [Fact]
    public async Task Open_rejects_unknown_account_type()
    {
        await SeedCustomerAsync("usr_a", "a@example.com");
        await AssertBusinessCode(
            () => _sut.OpenAsync(new OpenAccountRequest("usr_a", "MagicPixie", "USD")),
            "INVALID_ACCOUNT_TYPE");
    }

    [Fact]
    public async Task Open_rejects_bad_currency()
    {
        await SeedCustomerAsync("usr_a", "a@example.com");
        await AssertBusinessCode(
            () => _sut.OpenAsync(new OpenAccountRequest("usr_a", "Checking", "USDT")),
            "INVALID_CURRENCY");
    }

    [Fact]
    public async Task Open_rejects_unknown_customer()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.OpenAsync(new OpenAccountRequest("nobody", "Checking", "USD")));
    }

    [Fact]
    public async Task Close_is_blocked_while_balance_is_not_zero()
    {
        var account = await SeedAccountWithBalanceAsync(120m);

        var ex = await Record.ExceptionAsync(() => _sut.CloseAsync(account.Id));
        var bre = Assert.IsType<BusinessRuleException>(ex);
        Assert.Equal("BALANCE_NOT_ZERO", bre.Code);
        Assert.Equal(409, bre.StatusCode);
    }

    [Fact]
    public async Task Close_succeeds_after_zeroing_and_marks_closed()
    {
        var account = await SeedAccountWithBalanceAsync(0m);

        await _sut.CloseAsync(account.Id);

        var stored = await _db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal(AccountStatus.Closed, stored.Status);
        Assert.NotNull(stored.ClosedAt);
    }

    [Fact]
    public async Task Ledger_returns_newest_first_paged()
    {
        var account = await SeedAccountWithBalanceAsync(0m);
        for (var i = 0; i < 12; i++)
        {
            _db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(), AccountId = account.Id, Type = TransactionType.Deposit,
                Amount = 10m + i, BalanceAfter = 10m + i, Reference = $"REF-{i:00}",
                CreatedAt = _clock.Now.AddMinutes(-i)
            });
        }
        await _db.SaveChangesAsync();

        var page1 = await _sut.GetLedgerAsync(account.Id, page: 1, pageSize: 10);
        var page2 = await _sut.GetLedgerAsync(account.Id, page: 2, pageSize: 10);

        Assert.Equal(12, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal("REF-10", page2.Items.First().Reference); // newest first
    }

    [Fact]
    public async Task Total_balance_excludes_closed_accounts()
    {
        var customer = await SeedCustomerAsync("usr_b", "b@example.com");
        var open = new Account { Id = Guid.NewGuid(), CustomerId = customer.Id, Number = "FB-1001", Type = AccountType.Checking, Balance = 900m };
        var closed = new Account { Id = Guid.NewGuid(), CustomerId = customer.Id, Number = "FB-1002", Type = AccountType.Savings, Balance = 5_000m, Status = AccountStatus.Closed };
        _db.Accounts.AddRange(open, closed);
        await _db.SaveChangesAsync();

        Assert.Equal(900m, await _sut.GetCustomerTotalBalanceAsync(customer.Id));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<Customer> SeedCustomerAsync(string externalId, string email)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(), ExternalId = externalId,
            FullName = "Test Customer", Email = email
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    private async Task<Account> SeedAccountWithBalanceAsync(decimal balance)
    {
        var customer = await SeedCustomerAsync($"usr_{Guid.NewGuid():N}", $"t{Guid.NewGuid():N}@example.com");
        var account = new Account
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, Number = $"FB-{Random.Shared.Next(10_000, 99_999)}",
            Type = AccountType.Checking, Balance = balance
        };
        _db.Accounts.Add(account);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw; }
        return account;
    }

    private static async Task AssertBusinessCode(Func<Task> action, string expectedCode, int expectedStatus = 400)
    {
        var ex = await Record.ExceptionAsync(action);
        var bre = Assert.IsType<BusinessRuleException>(ex);
        Assert.Equal(expectedCode, bre.Code);
        Assert.Equal(expectedStatus, bre.StatusCode);
    }
}
