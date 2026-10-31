namespace FinTrack.Api.Auth;

/// <summary>Configuration block for Azure AD B2C single sign-on (production auth mode).</summary>
public sealed class AzureAdB2COptions
{
    public const string SectionName = "AzureAdB2C";

    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>Directory id or verified domain, e.g. contoso.onmicrosoft.com.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Web API application (client) id — the audience of issued access tokens.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>User flow, e.g. B2C_1_susi.</summary>
    public string SignUpSignInPolicyId { get; set; } = "B2C_1_susi";
}

/// <summary>Configuration for the development/demo JWT issuer (NOT used in production).</summary>
public sealed class DevelopmentJwtOptions
{
    public string Issuer { get; set; } = "fintrack.dev";
    public string Audience { get; set; } = "fintrack.api";
    public string SigningKeyBase64 { get; set; } = string.Empty;
    public int TokenLifetimeMinutes { get; set; } = 120;
}
