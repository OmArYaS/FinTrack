using FinTrack.Application.Common.Exceptions;
using FinTrack.Application.Common.Interfaces;
using FinTrack.Application.Contracts;
using FinTrack.Domain.Enums;
using FinTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Accounts;

public interface ITransferService
{
    Task<TransferResultDto> ExecuteAsync(TransferRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Posts an intra-bank transfer atomically:
///   1. validates fraud limits (per-transaction cap, rolling 24h daily outflow cap),
///   2. re-checks availability inside a serialized transaction,
///   3. writes two ledger entries and updates balances,
///   4. relies on the Account rowversion for optimistic concurrency so no lost update
///      can occur between the balance check and SaveChanges.
/// The same invariants are enforced server-side by dbo.usp_PostTransfer for batch loads.
/// </summary>
public sealed class TransferService(
    IApplicationDbContext db,
    IDateTime dateTime,
    TransferLimits limits,
    ICurrentUser currentUser) : ITransferService
{
    public async Task<TransferResultDto> ExecuteAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);

        await using var scope = await db.BeginTransactionAsync(cancellationToken);

        var from = await db.Accounts
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == request.FromAccountId, cancellationToken)
            ?? throw new NotFoundException("Account", request.FromAccountId);

        // Ownership/delegation is checked on the SOURCE account before
        // even resolving the destination (no side-channel probing).
        if (!currentUser.IsStaff && from.Customer.ExternalId != currentUser.ExternalId)
        {
            throw new BusinessRuleException("NOT_ACCOUNT_OWNER", "Caller does not own the source account.", 403);
        }

        var to = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.ToAccountId, cancellationToken)
            ?? throw new NotFoundException("Account", request.ToAccountId);

        EnsureTransferable(from);
        EnsureTransferable(to);

        if (from.Currency != to.Currency)
        {
            throw new BusinessRuleException("CURRENCY_MISMATCH",
                $"Cannot transfer {from.Currency} into a {to.Currency} account.");
        }

        var dailyOutflow = await db.Transactions
            .Where(t => t.AccountId == from.Id
                        && t.Type == TransactionType.TransferOut
                        && t.CreatedAt >= dateTime.Now.AddDays(-1))
            .SumAsync(t => -t.Amount, cancellationToken);

        if (dailyOutflow + request.Amount > limits.MaxDailyOutflowPerAccount)
        {
            throw new BusinessRuleException("EXCEEDS_DAILY_LIMIT",
                $"Daily outflow limit of {limits.MaxDailyOutflowPerAccount} {from.Currency} would be exceeded.", 403);
        }

        if (from.Balance < request.Amount)
        {
            throw new BusinessRuleException("INSUFFICIENT_FUNDS",
                $"Account {from.Number} has {from.Balance:0.00} {from.Currency}; {request.Amount:0.00} requested.", 409);
        }

        var reference = $"TRF-{Guid.NewGuid():N}";

        from.Balance -= request.Amount;
        to.Balance += request.Amount;

        var now = dateTime.Now;
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = from.Id,
            Type = TransactionType.TransferOut,
            Amount = -request.Amount,
            BalanceAfter = from.Balance,
            Reference = reference,
            Description = request.Description ?? string.Empty,
            CreatedAt = now
        });
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = to.Id,
            Type = TransactionType.TransferIn,
            Amount = request.Amount,
            BalanceAfter = to.Balance,
            Reference = reference,
            Description = request.Description ?? string.Empty,
            CreatedAt = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await scope.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A competing request mutated a balance in between check & save.
            throw new BusinessRuleException("CONCURRENT_UPDATE",
                "Account balance changed concurrently; retry the operation.", 409);
        }

        return new TransferResultDto(reference, from.Balance, to.Balance);
    }

    private void Validate(TransferRequest request)
    {
        if (request.FromAccountId == request.ToAccountId)
        {
            throw new BusinessRuleException("SELF_TRANSFER", "Source and destination accounts must be different.");
        }

        if (request.Amount <= 0)
        {
            throw new BusinessRuleException("INVALID_AMOUNT", "Transfer amount must be greater than zero.");
        }

        if (request.Amount > limits.MaxAmountPerTransaction)
        {
            throw new BusinessRuleException("EXCEEDS_TRANSACTION_LIMIT",
                $"Single transfers cannot exceed {limits.MaxAmountPerTransaction:0.00}.", 400);
        }
    }

    private void EnsureTransferable(Account account)
    {
        if (account.Status == AccountStatus.Closed)
        {
            throw new BusinessRuleException("ACCOUNT_CLOSED", $"Account {account.Number} is closed.", 409);
        }

        if (account.Status == AccountStatus.Frozen)
        {
            throw new BusinessRuleException("ACCOUNT_FROZEN", $"Account {account.Number} is frozen.", 403);
        }
    }
}
