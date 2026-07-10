using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace eProcure.Infrastructure.Persistence;

/// <summary>
/// Used by `dotnet ef` at design time so migration commands don't run the API's
/// startup pipeline. Reads the connection string from the ConnectionStrings__Default
/// environment variable, falling back to the well-known local dev string from
/// SETUP.md / docker-compose.yml (the local password is not a production secret).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Design-time only: used by `dotnet ef` (migrations/scaffolding) against the local Docker
        // Postgres. This is NOT a runtime code path — the running API always binds its connection
        // string from configuration. The literal below is the local container's credential
        // (docker-compose.yml), never a production secret.
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
