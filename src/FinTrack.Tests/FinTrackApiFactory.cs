using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinTrack.Tests;

/// <summary>
/// Contract-test fixture. The application itself is provider-aware (Database:Provider),
/// so this factory only pins the SQLite demo mode — no DbContext registrations are
/// overridden, avoiding the classic "two EF providers in one host" test failure.
/// Program's DatabaseInitializer guarantees schema + demo data before serving traffic.
/// </summary>
public sealed class FinTrackApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Provider", "Sqlite");
    }
}

/// <summary>Fixed clock for deterministic unit tests of clock-sensitive rules.</summary>
public sealed class FixedDateTime(DateTime now) : Application.Common.Interfaces.IDateTime
{
    public DateTime Now => now;
}
