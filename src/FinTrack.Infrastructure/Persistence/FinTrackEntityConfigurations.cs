using FinTrack.Domain.Entities;
using FinTrack.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Infrastructure.Persistence;

/// <summary>
/// Central entity configuration — mirrors database/scripts/*.sql so both
/// code-first migrations and the DBA-reviewed scripts agree on the same shapes.
/// </summary>
public static class FinTrackEntityConfigurations
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ExternalId).HasMaxLength(64);
            entity.Property(c => c.FullName).HasMaxLength(200);
            entity.Property(c => c.Email).HasMaxLength(254);
            entity.HasIndex(c => c.Email).IsUnique();
            entity.HasIndex(c => c.ExternalId).IsUnique();
            entity.HasMany(c => c.Accounts)
                .WithOne(a => a.Customer)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Number).HasMaxLength(24);
            entity.Property(a => a.Currency).HasMaxLength(3);
            entity.Property(a => a.Balance).HasPrecision(18, 2);
            entity.Property(a => a.RowVersion).IsRowVersion();
            entity.HasIndex(a => a.Number).IsUnique();
            entity.HasIndex(a => a.CustomerId);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Amount).HasPrecision(18, 2);
            entity.Property(t => t.BalanceAfter).HasPrecision(18, 2);
            entity.Property(t => t.Reference).HasMaxLength(64);
            entity.Property(t => t.Description).HasMaxLength(256);
            entity.HasIndex(t => new { t.AccountId, t.CreatedAt });
            entity.HasIndex(t => new { t.AccountId, t.Reference }).IsUnique();
            entity.HasOne(t => t.Account)
                .WithMany()
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
