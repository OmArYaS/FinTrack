namespace FinTrack.Domain.Auditing;

/// <summary>
/// Base entity carrying creation metadata. Set by <c>FinTrackDbContext</c> via SaveChanges overrides.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>UTC creation time. Set by FinTrackDbContext.SaveChanges when unchanged.</summary>
    public DateTime CreatedAt { get; set; }
}
