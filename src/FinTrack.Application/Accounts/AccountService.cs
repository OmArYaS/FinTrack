using FinTrack.Application.Common.Exceptions;
using FinTrack.Application.Common.Interfaces;
using FinTrack.Application.Common.Models;
using FinTrack.Application.Contracts;
using FinTrack.Domain.Entities;
using FinTrack.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Accounts;

public interface IAccountService
{
    Task<AccountSummaryDto> OpenAsync(OpenAccountRequest request, CancellationToken cancellationToken = default);

    Task CloseAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountSummaryDto>> GetCustomerAccountsAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Staff-only listing across all customers.</summary>
    Task<IReadOnlyList<AccountSummaryDto>> GetStaffOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Caller-scoped listing: staff may browse anyone, retail callers are pinned to their own customer.</summary>
    Task<IReadOnlyList<AccountSummaryDto>> GetForCallerAsync(ICurrentUser caller, Guid? requestedCustomerId, CancellationToken cancellationToken = default);

    Task<AccountSummaryDto> GetAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<PagedResult<LedgerEntryDto>> GetLedgerAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<decimal> GetCustomerTotalBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);
}

/// <summary>Use cases for the accounts module. All reads/writes are expressible over <see cref="IApplicationDbContext"/>.</summary>
public sealed class AccountService(IApplicationDbContext db, IDateTime dateTime) : IAccountService
{
    public async Task<AccountSummaryDto> OpenAsync(OpenAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerExternalId))
        {
            throw new BusinessRuleException("CUSTOMER_REQUIRED", "Customer id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountType) ||
            !Enum.TryParse<AccountType>(request.AccountType, ignoreCase: true, out var type) || type == 0)
        {
            throw new BusinessRuleException("INVALID_ACCOUNT_TYPE", $"Unknown account type '{request.AccountType}'.");
        }

        var currency = request.Currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currency.Length != 3)
        {
            throw new BusinessRuleException("INVALID_CURRENCY", "Currency must be a 3-letter ISO-4217 code.");
        }

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.ExternalId == request.CustomerExternalId, cancellationToken)
            ?? throw new NotFoundException("Customer", request.CustomerExternalId);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Number = await NextNumberAsync(cancellationToken),
            Type = type,
            Status = AccountStatus.Active,
            Currency = currency,
            Balance = 0m,
            OpenedAt = dateTime.Now,
            CreatedAt = dateTime.Now
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(account);
    }

    public async Task CloseAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new NotFoundException("Account", accountId);

        if (account.Status == AccountStatus.Closed)
        {
            throw new BusinessRuleException("ALREADY_CLOSED", $"Account {account.Number} is already closed.");
        }

        if (account.Balance != 0m)
        {
            throw new BusinessRuleException("BALANCE_NOT_ZERO",
                $"Account {account.Number} must be zeroed before closing (current {account.Balance:0.00}).", 409);
        }

        account.Status = AccountStatus.Closed;
        account.ClosedAt = dateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> GetCustomerAccountsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var accounts = await db.Accounts
            .Where(a => a.CustomerId == customerId)
            .OrderBy(a => a.OpenedAt)
            .ToListAsync(cancellationToken);

        return accounts.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> GetStaffOverviewAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await db.Accounts
            .OrderBy(a => a.Number)
            .ToListAsync(cancellationToken);

        return accounts.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> GetForCallerAsync(
        ICurrentUser caller, Guid? requestedCustomerId, CancellationToken cancellationToken = default)
    {
        if (caller.IsStaff)
        {
            return requestedCustomerId is null
                ? await GetStaffOverviewAsync(cancellationToken)
                : await GetCustomerAccountsAsync(requestedCustomerId.Value, cancellationToken);
        }

        var own = await db.Customers
            .FirstOrDefaultAsync(c => c.ExternalId == caller.ExternalId, cancellationToken)
            ?? throw new NotFoundException("Customer", caller.ExternalId);

        if (requestedCustomerId is Guid requested && requested != own.Id)
        {
            throw new BusinessRuleException("NOT_ACCOUNT_OWNER", "Retail callers may only list their own accounts.", 403);
        }

        return await GetCustomerAccountsAsync(own.Id, cancellationToken);
    }

    public async Task<AccountSummaryDto> GetAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new NotFoundException("Account", accountId);
        return ToDto(account);
    }

    public async Task<PagedResult<LedgerEntryDto>> GetLedgerAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);

        if (!await db.Accounts.AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            throw new NotFoundException("Account", accountId);
        }

        var query = db.Transactions
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new LedgerEntryDto(
                t.Id, t.Type.ToString(), t.Amount, t.BalanceAfter, t.Reference,
                t.Description, t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<LedgerEntryDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<decimal> GetCustomerTotalBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await db.Accounts
            .Where(a => a.CustomerId == customerId && a.Status != AccountStatus.Closed)
            .SumAsync(a => (decimal?)a.Balance, cancellationToken) ?? 0m;
    }

    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        // Time-keyed, collision-checked account number generator (demo-friendly).
        // In production this is reserved up-front inside the open-account procedure.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"FB-{HHMMSS()}{attempt:00}{Random.Shared.Next(100, 900):D3}";
            if (!await db.Accounts.AnyAsync(a => a.Number == candidate, ct))
            {
                return candidate;
            }
        }

        throw new BusinessRuleException("NUMBER_GENERATION", "Could not allocate a unique account number, retry.");
    }

    private static string HHMMSS() => DateTime.UtcNow.ToString("dHHmmss");

    private static AccountSummaryDto ToDto(Account a) => new(
        a.Id, a.CustomerId, a.Number, a.Type.ToString(), a.Status.ToString(), a.Currency, a.Balance, a.OpenedAt);
}
