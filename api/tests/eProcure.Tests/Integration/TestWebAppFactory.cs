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
public sealed class TestWebAppFactory(
    string environment = "Testing", bool demoEnabled = true,
    IEnumerable<eProcure.Api.Auth.DevUser>? extraDevUsers = null)
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

            // Role-matrix suite: append pure per-role test principals (no seeded dev user holds
            // Approver alone). The shipped DevUserStore list is untouched outside tests.
            if (extraDevUsers is not null)
                services.AddSingleton(new eProcure.Api.Auth.DevUserStore(extraDevUsers));
        });
    }

    /// <summary>Seed the in-memory store before exercising the host. The D7 schema
    /// invariants (numbering schemes + Standard PR Form — migration-guaranteed in
    /// production) ride along idempotently so mint/submit paths behave identically.</summary>
    /// <summary>TEST-SWEEP-T2: first line id of a PO — the per-line segment grain needs a real line.</summary>
    public async Task<Guid> LineIdOf(Guid poId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines).FirstAsync(p => p.Id == poId)).Lines.First().Id;
    }

    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.NumberingSchemes.Any())
        {
            db.NumberingSchemes.AddRange(eProcure.Application.Forms.EntryFormSeed.ToSchemeEntities());
            var (form, subtabs, groups, fields) = eProcure.Application.Forms.EntryFormSeed.ToStandardPrFormEntities();
            db.EntryFormDefs.Add(form);
            db.EntryFormSubtabs.AddRange(subtabs);
            db.EntryFormGroups.AddRange(groups);
            db.EntryFormFields.AddRange(fields);
            foreach (var (sfType, sfCode, sfName, sfRows) in eProcure.Application.Forms.EntryFormSeed.StandardForms)
            {
                var (sfDef, sfSubtabs, sfGroups, sfFields) = eProcure.Application.Forms.EntryFormSeed.ToStandardFormEntities(sfType, sfCode, sfName, sfRows);
                db.EntryFormDefs.Add(sfDef);
                db.EntryFormSubtabs.AddRange(sfSubtabs);
                db.EntryFormGroups.AddRange(sfGroups);
                db.EntryFormFields.AddRange(sfFields);
            }
            await db.SaveChangesAsync();
        }
        await seed(db);
        await db.SaveChangesAsync();
    }
}
