using FinTrack.Domain.Entities;
using FinTrack.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinTrack.Infrastructure.Persistence;

/// <summary>
/// Applies code-first migrations on startup (MS SQL Server) or creates a
/// disposable SQLite demo database, then seeds showcase data when the store is empty.
/// </summary>
public sealed class DatabaseInitializer(FinTrackDbContext db, IConfiguration configuration, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var pending = await db.Database.GetPendingMigrationsAsync(ct);
            logger.LogInformation("Applying {Count} pending EF Core migration(s)…", pending.Count());
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        await SeedShowcaseDataAsync(ct);
    }

    private async Task SeedShowcaseDataAsync(CancellationToken ct)
    {
        if (await db.Customers.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded; skipping demo data.");
            return;
        }

        var amira = new Customer
        {
            Id = Guid.NewGuid(),
            ExternalId = "usr_amira",
            FullName = "Amira Hassan",
            Email = "amira.hassan@example.com",
            CreatedAt = DateTime.UtcNow
        };
        var tarek = new Customer
        {
            Id = Guid.NewGuid(),
            ExternalId = "usr_tarek",
            FullName = "Tarek Samir",
            Email = "tarek.samir@example.com",
            CreatedAt = DateTime.UtcNow
        };

        var checking = new Account
        {
            Id = Guid.NewGuid(), CustomerId = amira.Id, Number = "FB-1029384756",
            Type = AccountType.Checking, Status = AccountStatus.Active,
            Currency = "USD", Balance = 4_250.00m,
            OpenedAt = DateTime.UtcNow.AddMonths(-18)
        };
        var savings = new Account
        {
            Id = Guid.NewGuid(), CustomerId = amira.Id, Number = "FB-5647382910",
            Type = AccountType.Savings, Status = AccountStatus.Active,
            Currency = "USD", Balance = 12_800.50m,
            OpenedAt = DateTime.UtcNow.AddMonths(-14)
        };
        var tarekChecking = new Account
        {
            Id = Guid.NewGuid(), CustomerId = tarek.Id, Number = "FB-7483920165",
            Type = AccountType.Checking, Status = AccountStatus.Active,
            Currency = "USD", Balance = 3_120.75m,
            OpenedAt = DateTime.UtcNow.AddMonths(-6)
        };

        db.Customers.AddRange(amira, tarek);
        db.Accounts.AddRange(checking, savings, tarekChecking);
        db.Transactions.AddRange(
            Ledger(checking, TransactionType.Deposit, 4000m, 4000m, "SEED-001", "Initial deposit", -18),
            Ledger(checking, TransactionType.Deposit, 250m, 4250m, "SEED-002", "Salary", -3),
            Ledger(savings, TransactionType.Deposit, 12800.50m, 12800.50m, "SEED-003", "Savings transfer", -14),
            Ledger(tarekChecking, TransactionType.Deposit, 3200m, 3200m, "SEED-004", "Initial deposit", -6),
            Ledger(tarekChecking, TransactionType.Fee, -79.25m, 3120.75m, "SEED-005", "Maintenance fee", -2));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded demo customers and accounts.");
    }

    private static Transaction Ledger(Account account, TransactionType type, decimal amount, decimal balanceAfter, string reference, string description, int monthsAgo) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = account.Id,
        Type = type,
        Amount = amount,
        BalanceAfter = balanceAfter,
        Reference = reference,
        Description = description,
        CreatedAt = DateTime.UtcNow.AddMonths(monthsAgo)
    };
}
