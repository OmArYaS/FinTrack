using FinTrack.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FinTrack.Infrastructure.Security;

/// <summary>Resolves the authenticated caller from the HTTP context (Azure AD B2C or dev stub).</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string ExternalId =>
        Principal?.FindFirst("oid")?.Value
        ?? Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? Principal?.FindFirst("sub")?.Value
        ?? string.Empty;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
