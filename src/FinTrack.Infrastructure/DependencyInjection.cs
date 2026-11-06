using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FinTrack.Infrastructure.Persistence;

namespace FinTrack.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence + cross-cutting infrastructure.
    /// Database:Provider = SqlServer (production / MS SQL Server) | Sqlite (zero-setup demo).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString(provider)
            ?? throw new InvalidOperationException($"Connection string '{provider}' is not configured.");

        services.AddDbContext<FinTrackDbContext>(options =>
            _ = provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? options.UseSqlServer(connectionString)
                : options.UseSqlite(connectionString));

        services.AddScoped<FinTrack.Application.Common.Interfaces.IApplicationDbContext>(sp =>
            sp.GetRequiredService<FinTrackDbContext>());

        services.AddScoped<FinTrack.Application.Common.Interfaces.IDateTime, FinTrack.Infrastructure.Common.DateTimeProvider>();
        services.AddScoped<FinTrack.Application.Common.Interfaces.ICurrentUser, FinTrack.Infrastructure.Security.CurrentUser>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
