using eProcure.Application.Abstractions;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

/// <summary>A controllable clock for deterministic timestamp/deadline tests.</summary>
public sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}

/// <summary>A mutable principal for audit-actor / masking / scoping tests.</summary>
public sealed class FakeCurrentUser(
    string? userId = "u_test",
    string? userName = "Test User",
    string[]? roles = null,
    Guid? vendorId = null) : ICurrentUser
{
    public bool IsAuthenticated => UserId is not null;
    public string? UserId { get; set; } = userId;
    public string? UserName { get; set; } = userName;
    public IReadOnlyCollection<string> Roles { get; set; } = roles ?? [];
    public Guid? VendorId { get; set; } = vendorId;
}

public static class TestDb
{
    /// <summary>A fresh in-memory AppDbContext with a unique store per call. Carries the
    /// D7 schema INVARIANTS the production migration guarantees (7 numbering schemes +
    /// the Standard PR Form) so the scheme-consulting mint and the submit guard behave
    /// identically to a migrated database.</summary>
    public static AppDbContext NewContext()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"eprocure-tests-{Guid.NewGuid()}")
            .Options);
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
        db.SaveChanges();
        return db;
    }
}

/// <summary>Bundles a fresh DbContext + the cross-cutting services for service tests.</summary>
public sealed class TestContext
{
    public required AppDbContext Db { get; init; }
    public required FakeClock Clock { get; init; }
    public required FakeCurrentUser User { get; init; }
    public required eProcure.Infrastructure.Services.CodeGenerator Codes { get; init; }
    public required eProcure.Infrastructure.Services.AuditLogWriter Audit { get; init; }

    public static TestContext New(string? actorId = "u_faridah", string? actorName = "Faridah Yusof")
    {
        var db = TestDb.NewContext();
        var clock = new FakeClock(new DateTime(2026, 6, 28, 9, 0, 0, DateTimeKind.Utc));
        var user = new FakeCurrentUser(actorId, actorName);
        return new TestContext
        {
            Db = db,
            Clock = clock,
            User = user,
            Codes = new eProcure.Infrastructure.Services.CodeGenerator(db, clock),
            Audit = new eProcure.Infrastructure.Services.AuditLogWriter(db, clock, user),
        };
    }
}

/// <summary>Test helper: attach seeded RfqInvitation rows to an Rfq (replaces the retired
/// InvitedVendorIds delimited column in test setups). Returns the same rfq for chaining.</summary>
public static class RfqTestExtensions
{
    public static eProcure.Domain.Sourcing.Rfq WithInvites(
        this eProcure.Domain.Sourcing.Rfq rfq, DateTime invitedUtc, params Guid[] vendorIds)
    {
        foreach (var v in vendorIds)
            rfq.Invitations.Add(eProcure.Domain.Sourcing.RfqInvitation.Seed(
                rfq.Id, v, eProcure.Domain.Sourcing.RfqInvitationStatus.Invited, invitedUtc));
        return rfq;
    }
}
