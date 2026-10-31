namespace FinTrack.Application.Common.Interfaces;

/// <summary>Caller identity resolved from ASP.NET Core authentication (Azure AD B2C or the development stub).</summary>
public interface ICurrentUser
{
    /// <summary>Identity-provider subject id (AAD B2C object id) or synthetic dev id.</summary>
    string ExternalId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);

    /// <summary>Staff = back-office employees (Teller/Admin) allowed to operate on behalf of customers.</summary>
    bool IsStaff => IsInRole("Teller") || IsInRole("Admin");
}
