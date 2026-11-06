using FinTrack.Application.Common.Interfaces;
using FinTrack.Domain.Auditing;
using FinTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Infrastructure.Persistence;

/// <summary>
/// Code-first model for MS SQL Server (migrations) and SQLite (zero-setup demo).
/// Model configuration lives in <see cref="FinTrackEntityConfigurations"/>.
/// </summary>
public sealed class FinTrackDbContext(DbContextOptions<FinTrackDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    public bool SupportsTransactions => Database.IsSqlServer() || Database.IsSqlite();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => FinTrackEntityConfigurations.Configure(modelBuilder);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!SupportsTransactions)
        {
            return new NoOpTransactionScope();
        }

        await Database.BeginTransactionAsync(cancellationToken);
        return new RelationalTransactionScope(this);
    }

    private sealed class RelationalTransactionScope(FinTrackDbContext context) : ITransactionScope
    {
        private bool _committed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await context.Database.CommitTransactionAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_committed)
            {
                return;
            }

            try
            {
                await context.Database.RollbackTransactionAsync();
            }
            catch (InvalidOperationException)
            {
                // Transaction already ended (e.g. completed via SaveChanges failure) — safe to ignore.
            }
        }
    }

    private sealed class NoOpTransactionScope : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
