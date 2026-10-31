using System.Text.Json;
using FinTrack.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

/// <summary>
/// Development/demo identity endpoint standing in for Azure AD B2C. Disabled when
/// Auth:Mode=AzureAdB2C or outside Development so it can never be reached in production.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AuthController(IDevTokenIssuer tokenIssuer, IConfiguration configuration) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Issues a development access token for a configured demo user.</summary>
    [HttpPost("token")]
    public IActionResult Token([FromBody] JsonElement body)
    {
        if (IsProductionAuth())
        {
            return NotFound();
        }

        string userName;
        if (body.ValueKind == JsonValueKind.String)
        {
            userName = body.GetString() ?? string.Empty;
        }
        else if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("userName", out var prop))
        {
            userName = prop.GetString() ?? string.Empty;
        }
        else
        {
            return BadRequest(new { code = "INVALID_BODY", detail = "Expected { \"userName\": \"...\" }." });
        }

        var loaded = configuration.GetSection("Auth:Development:Users")
            .Get<Dictionary<string, DemoUserConfig>>() ?? new Dictionary<string, DemoUserConfig>();
        var users = new Dictionary<string, DemoUserConfig>(loaded, StringComparer.OrdinalIgnoreCase);

        if (!users.TryGetValue(userName, out var profile))
        {
            return Unauthorized(new { code = "UNKNOWN_USER", detail = $"Demo user '{userName}' is not configured." });
        }

        var token = tokenIssuer.IssueToken(userName, profile.ExternalId, userName, profile.Roles);
        return Ok(new AuthTokenResponse(token, "Bearer", 3600));
    }

    private bool IsProductionAuth() =>
        string.Equals(configuration["Auth:Mode"], "AzureAdB2C", StringComparison.OrdinalIgnoreCase);

    private sealed record DemoUserConfig
    {
        public string ExternalId { get; set; } = string.Empty;
        public string[] Roles { get; set; } = Array.Empty<string>();
    }

    public sealed record AuthTokenResponse(string AccessToken, string TokenType, int ExpiresIn);
}
