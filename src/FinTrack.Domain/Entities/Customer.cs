using FinTrack.Domain.Auditing;
using FinTrack.Domain.Enums;

namespace FinTrack.Domain.Entities;

/// <summary>Individual or corporate banking customer.</summary>
public sealed class Customer : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Stable identifier issued by identity provider (Azure AD B2C object id) when federated.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
