using FinTrack.Domain.Auditing;
using FinTrack.Domain.Enums;

namespace FinTrack.Domain.Entities;

/// <summary>
/// Bank account. Balances are posted exclusively through the ledger (<see cref="Transaction"/>)
/// so the balance and its audit trail can never diverge.
/// </summary>
public sealed class Account : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    /// <summary>Human readable account number, e.g. FB-0000427301.</summary>
    public string Number { get; set; } = string.Empty;

    public AccountType Type { get; set; }

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    /// <summary>ISO-4217 code. Transfers are only allowed within one currency.</summary>
    public string Currency { get; set; } = "USD";

    public decimal Balance { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>SQL Server rowversion enabling optimistic concurrency on balance updates.
    /// Nullable in the CLR model because value is database-generated (always present in SQL Server).</summary>
    public byte[]? RowVersion { get; set; }
}
