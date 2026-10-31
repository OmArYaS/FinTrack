using FinTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FinTrack.Application.Common.Interfaces;

/// <summary>
/// Persistence contract consumed by the Application layer. Implemented by
/// FinTrack.Infrastructure's <c>FinTrackDbContext</c>. Offers EF Core's
/// DbSets + SaveChanges but hides the concrete provider.
/// </summary>
public interface IApplicationDbContext
{
    bool SupportsTransactions { get; }

    DbSet<Customer> Customers { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Transaction> Transactions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins a provider-backed transaction. Returns a no-op scope on providers without transaction support (e.g. EF InMemory).</summary>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
