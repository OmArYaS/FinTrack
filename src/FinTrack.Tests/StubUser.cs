using FinTrack.Application.Common.Interfaces;

namespace FinTrack.Tests;

/// <summary>In-memory stand-in for the HttpContext-backed ICurrentUser of the API.</summary>
public sealed class StubUser(string externalId, string[] roles) : ICurrentUser
{
    public string ExternalId { get; } = externalId;

    public bool IsAuthenticated => true;

    public bool IsInRole(string role) => roles.Contains(role);
}
