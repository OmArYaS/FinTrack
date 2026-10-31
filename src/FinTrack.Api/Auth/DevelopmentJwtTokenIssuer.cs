using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinTrack.Api.Auth;

public interface IDevTokenIssuer
{
    string IssueToken(string userName, string externalId, string displayName, IEnumerable<string> roles);
}

/// <summary>
/// Local development identity stub standing in for Azure AD B2C so the entire
/// stack runs and integrates without a tenant dependency. Never enabled in
/// production (see docs/security.md — Auth:Mode=AzureAdB2C is the prod path).
/// </summary>
public sealed class DevelopmentJwtTokenIssuer(IOptions<DevelopmentJwtOptions> options) : IDevTokenIssuer
{
    public string IssueToken(string userName, string externalId, string displayName, IEnumerable<string> roles)
    {
        var o = options.Value;
        var key = new SymmetricSecurityKey(Convert.FromBase64String(o.SigningKeyBase64));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("name", userName),
            new("oid", string.IsNullOrWhiteSpace(externalId) ? userName : externalId),
            new("aud_scp", "api.access")
        };

        claims.AddRange(roles.Select(role => new Claim("roles", role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Expires = DateTime.UtcNow.AddMinutes(o.TokenLifetimeMinutes),
            SigningCredentials = credentials,
            Subject = new ClaimsIdentity(claims)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
