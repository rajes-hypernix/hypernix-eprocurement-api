using eProcure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eProcure.Tests.Integration;

/// <summary>
/// Boots the real API host for perimeter integration tests (the first WebApplicationFactory in the
/// repo). The Npgsql context is swapped for a shared in-memory store, and startup config is supplied
/// so Program's guards pass. The environment/demo flags are parameterised so a single harness covers
/// both the demo-active matrix (Testing + Demo enabled) and the demo-inactive regression (Production).
/// A non-Development environment also skips Program's migrate/seed block (unsupported on in-memory).
/// </summary>
public sealed class TestWebAppFactory(string environment = "Testing", bool demoEnabled = true)
    : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"slicef-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);

        // Read at builder time by Program (connection string + JWT key guards); UseSetting makes them
        // available immediately in Configuration.
        builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=unused;Username=u;Password=p");
        builder.UseSetting("Jwt:Key", "slice-f-test-signing-key-0123456789-abcdefghij");
        builder.UseSetting("Jwt:Issuer", "eprocure");
        builder.UseSetting("Jwt:Audience", "eprocure");
        builder.UseSetting("Demo:Enabled", demoEnabled ? "true" : "false");

        builder.ConfigureServices(services =>
        {
            // Replace the Npgsql AppDbContext with a shared in-memory store (no real DB in tests).
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AppDbContext) ||
                (d.ServiceType.FullName?.Contains("DbContextOptions") ?? false)).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>Seed the in-memory store before exercising the host.</summary>
    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }
}
