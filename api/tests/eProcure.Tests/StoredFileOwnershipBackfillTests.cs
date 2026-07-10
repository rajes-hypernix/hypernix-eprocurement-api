using eProcure.Domain.Files;
using eProcure.Domain.Onboarding;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Covers the StoredFileOwnership migration backfill against REAL rows (T5 follow-up). The dev seed
/// had no files when the migration ran, so the inference had never executed. This seeds one file of
/// each ownership class, runs the SAME statements the migration runs
/// (<see cref="StoredFileOwnershipBackfill.Statements"/> — extracted, not replicated), and asserts
/// each lands in the right OwnerKind/OwnerVendorId. Runs against the local Docker Postgres inside a
/// rolled-back transaction (the regex/lateral-join SQL is Postgres-only); nothing is left behind.
/// </summary>
public sealed class StoredFileOwnershipBackfillTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Backfill_attributes_each_ownership_class_and_leaves_orphans_Internal()
    {
        await using var db = NewCtx();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Backfill test requires the local Docker Postgres (docker compose up -d).");

        var sfx = Guid.NewGuid().ToString("N")[..8];
        await using var tx = await db.Database.BeginTransactionAsync();

        var vBid = new Vendor { Code = $"V-BID-{sfx}", Name = "BidCo", RegisteredName = "BidCo" };
        var vOnb = new Vendor { Code = $"V-ONB-{sfx}", Name = "OnbCo", RegisteredName = "OnbCo" };
        db.Vendors.AddRange(vBid, vOnb);

        // One file per ownership class — all start at the fail-closed default (OwnerKind = Internal).
        StoredFile File(string name) => new() { Name = name, ContentType = "application/pdf", Content = [1], Size = 1, CreatedUtc = Now };
        var fBid = File("bid.pdf");
        var fDoc = File("ssm.pdf");
        var fAns = File("answer.pdf");
        var fOrphan = File("orphan.pdf");   // referenced by nothing
        db.StoredFiles.AddRange(fBid, fDoc, fAns, fOrphan);

        // Bid attachment: fBid referenced via the "<fileId>::name" convention in a bid answer.
        var rfq = new Rfq { Code = $"RFQ-BF-{sfx}", Title = "Backfill", CreatedUtc = Now, UpdatedUtc = Now };
        db.Rfqs.Add(rfq);
        db.Bids.Add(new Bid
        {
            Code = $"BID-BF-{sfx}", RfqId = rfq.Id, VendorId = vBid.Id, Submitted = true, CreatedUtc = Now, UpdatedUtc = Now,
            Answers = { new BidAnswer { QuestionOrder = 0, Value = $"{fBid.Id}::bid.pdf" } },
        });

        // Onboarding: an application (promoted to vOnb) with a typed document (fDoc) + an answer file ref (fAns).
        var app = OnboardingTestData.Application($"VOB-BF-{sfx}");
        app.Documents.Add(new OnboardingDocument { Key = "ssm", FileName = "ssm.pdf", StoredFileId = fDoc.Id, UploadedUtc = Now });
        app.Answers.Add(new OnboardingAnswer { FormTemplateId = Guid.NewGuid(), QuestionOrder = 0, Value = $"{fAns.Id}::answer.pdf" });
        db.VendorOnboardingApplications.Add(app);

        await db.SaveChangesAsync();
        // PromotedVendorId has a private setter (set only on approval) — stamp it directly for the fixture.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""VendorOnboardingApplications"" SET ""PromotedVendorId"" = {vOnb.Id} WHERE ""Id"" = {app.Id}");

        // Run the exact statements the migration runs — via raw ADO so the regex braces ("{36}") are
        // not misread as string.Format placeholders (which is why the migration uses migrationBuilder.Sql).
        var conn = db.Database.GetDbConnection();
        var dbTx = db.Database.CurrentTransaction!.GetDbTransaction();
        foreach (var sql in StoredFileOwnershipBackfill.Statements)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = dbTx;
            await cmd.ExecuteNonQueryAsync();
        }

        var owners = await db.StoredFiles.AsNoTracking()
            .Where(f => new[] { fBid.Id, fDoc.Id, fAns.Id, fOrphan.Id }.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => new { f.OwnerKind, f.OwnerVendorId });

        owners[fBid.Id].OwnerKind.Should().Be(FileOwnerKind.Bid);
        owners[fBid.Id].OwnerVendorId.Should().Be(vBid.Id);

        owners[fDoc.Id].OwnerKind.Should().Be(FileOwnerKind.OnboardingDocument);
        owners[fDoc.Id].OwnerVendorId.Should().Be(vOnb.Id);

        owners[fAns.Id].OwnerKind.Should().Be(FileOwnerKind.OnboardingAnswer);
        owners[fAns.Id].OwnerVendorId.Should().Be(vOnb.Id);

        owners[fOrphan.Id].OwnerKind.Should().Be(FileOwnerKind.Internal, "an unattributable file stays fail-closed");
        owners[fOrphan.Id].OwnerVendorId.Should().BeNull();

        await tx.RollbackAsync();
    }
}
