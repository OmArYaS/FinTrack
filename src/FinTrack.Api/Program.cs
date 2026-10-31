using System.Security.Claims;
using System.Text.Json;
using FinTrack.Api.Auth;
using FinTrack.Api.Authorization;
using FinTrack.Api.Middleware;
using FinTrack.Application;
using FinTrack.Application.Accounts;
using FinTrack.Infrastructure;
using FinTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Layers ───────────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<TransferLimits>(
    builder.Configuration.GetSection("Banking:TransferLimits"));
builder.Services.AddSingleton<TransferLimits>(sp =>
    sp.GetRequiredService<IOptions<TransferLimits>>().Value);

// ── Authentication: Azure AD B2C (prod) or development JWT stub ──────────────
var authMode = builder.Configuration["Auth:Mode"] ?? "Development";

if (authMode.Equals("AzureAdB2C", StringComparison.OrdinalIgnoreCase))
{
    var b2c = builder.Configuration.GetSection(AzureAdB2COptions.SectionName).Get<AzureAdB2COptions>()
        ?? throw new InvalidOperationException("AzureAdB2C configuration section is missing.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; // keep raw AAD claim types (oid, roles)
            options.Authority = $"{b2c.Instance.TrimEnd('/')}/{b2c.TenantId}/{b2c.SignUpSignInPolicyId}/v2.0";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidAudience = b2c.ClientId,
                NameClaimType = "name",
                RoleClaimType = "roles",
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
}
else
{
    builder.Services.Configure<DevelopmentJwtOptions>(builder.Configuration.GetSection("Auth:Development"));
    builder.Services.AddSingleton<IDevTokenIssuer, DevelopmentJwtTokenIssuer>();

    var dev = builder.Configuration.GetSection("Auth:Development").Get<DevelopmentJwtOptions>()
        ?? throw new InvalidOperationException("Auth:Development configuration section is missing.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = dev.Issuer,
                ValidateAudience = true,
                ValidAudience = dev.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(dev.SigningKeyBase64)),
                NameClaimType = "name",
                RoleClaimType = "roles",
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
}

builder.Services.AddFinTrackAuthorization();
builder.Services.AddHttpContextAccessor();

// ── Web plumbing ─────────────────────────────────────────────────────────────
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "FinTrack Banking API", Version = "v1" }));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FinTrackDbContext>("sql", tags: new[] { "ready" });

var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FinTrack Banking API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

// ── Database: apply code-first migrations, then seed demo data ───────────────
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

/// <summary>Exposes Program for the WebApplicationFactory-based contract tests.</summary>
public partial class Program { }
