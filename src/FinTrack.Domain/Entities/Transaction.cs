using FinTrack.Domain.Auditing;
using FinTrack.Domain.Enums;

namespace FinTrack.Domain.Entities;

/// <summary>Append-only double-entry style ledger line (one entry per monetary movement).</summary>
public sealed class Transaction : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public TransactionType Type { get; set; }

    /// <summary>Signed amount; negative for money leaving the account.</summary>
    public decimal Amount { get; set; }

    /// <summary>Account balance immediately after this entry was posted.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>Grouping id; for transfers both sides share one unique reference.</summary>
    public string Reference { get; set; } = string.Empty;

    public string? Description { get; set; }
}
