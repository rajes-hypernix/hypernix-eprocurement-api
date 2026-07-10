using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class AuditLogWriterTests
{
    [Fact]
    public async Task WriteAsync_PersistsEntry_WithActorAndTimestampStamped()
    {
        using var db = TestDb.NewContext();
        var when = new DateTime(2026, 6, 28, 9, 30, 0, DateTimeKind.Utc);
        var clock = new FakeClock(when);
        var user = new FakeCurrentUser("u_faridah", "Faridah Yusof");
        var audit = new AuditLogWriter(db, clock, user);

        await audit.WriteAsync("Rfq", "RFQ-2026-0001", "Released",
            before: "Draft", after: "Open");

        var entry = await db.AuditEntries.SingleAsync();
        entry.EntityType.Should().Be("Rfq");
        entry.EntityId.Should().Be("RFQ-2026-0001");
        entry.Action.Should().Be("Released");
        entry.Before.Should().Be("Draft");
        entry.After.Should().Be("Open");
        entry.ActorId.Should().Be("u_faridah");
        entry.ActorName.Should().Be("Faridah Yusof");
        entry.UtcTimestamp.Should().Be(when);
        entry.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteAsync_AnonymousActor_FallsBackToSystem()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var user = new FakeCurrentUser(userId: null, userName: null);
        var audit = new AuditLogWriter(db, clock, user);

        await audit.WriteAsync("Po", "PO-2026-0001", "Issued");

        var entry = await db.AuditEntries.SingleAsync();
        entry.ActorId.Should().Be("system");
        entry.ActorName.Should().Be("System");
        entry.Before.Should().BeNull();
        entry.After.Should().BeNull();
    }

    [Fact]
    public async Task WriteAsync_MultipleEntries_AreAllAppended()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var audit = new AuditLogWriter(db, clock, new FakeCurrentUser());

        await audit.WriteAsync("Rfq", "RFQ-2026-0001", "Created");
        await audit.WriteAsync("Rfq", "RFQ-2026-0001", "Released");

        var entries = await db.AuditEntries.ToListAsync();
        entries.Should().HaveCount(2);
        entries.Select(e => e.Action).Should().Contain(["Created", "Released"]);
    }
}
