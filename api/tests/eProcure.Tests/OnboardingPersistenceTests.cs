using eProcure.Domain.Onboarding;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice A — integration tests (EF InMemory): the application round-trips with a VOB sequence code,
/// financial snapshot, answers and append-only rounds; the invitation stores only the token hash;
/// FormTemplate.Purpose filters onboarding packs; and a transition writes a typed AuditEntry.
/// (EDGE-CASES A1, D1, D4, F1, F4, and the audit seam.)
/// </summary>
public class OnboardingPersistenceTests
{
    private static readonly DateTime T = OnboardingTestData.T;

    [Fact]
    public async Task Application_RoundTrips_WithSequenceCode_Snapshot_AndAnswers()   // A1, D4
    {
        var ctx = TestContext.New();
        var db = ctx.Db;

        var code = await ctx.Codes.NextAsync("VOB");
        code.Should().MatchRegex(@"^VOB-2026-\d{4}$");                    // VOB-2026-#### from NumberSequence

        var app = OnboardingTestData.Application(code, VendorType.NonSwec, withFinancials: true);
        var templateId = Guid.NewGuid();
        app.Answers.Add(new OnboardingAnswer { FormTemplateId = templateId, QuestionOrder = 0, Value = "Yes" });
        app.MarkInProgress(T);
        app.Submit(T);                                                    // captures the submit snapshot
        db.VendorOnboardingApplications.Add(app);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var loaded = await db.VendorOnboardingApplications
            .Include(a => a.Financial).ThenInclude(f => f!.Snapshots)
            .Include(a => a.Financial).ThenInclude(f => f!.Years)
            .FirstAsync(a => a.Code == code);

        loaded.Status.Should().Be(OnboardingStatus.Submitted);
        loaded.Answers.Should().ContainSingle(x => x.FormTemplateId == templateId && x.Value == "Yes");
        loaded.Financial!.Years.Should().HaveCount(3);
        loaded.Financial.Snapshots.Should().ContainSingle(s => s.Stage == FinancialSnapshotStage.AtSubmit);
        loaded.Financial.Snapshots[0].Band.Should().Be(FinancialBand.C);
        loaded.Financial.Snapshots[0].WeightedZ.Should().BeApproximately(1.258645, 1e-6);
    }

    [Fact]
    public async Task Invitation_Persists_OnlyTheTokenHash()   // F1
    {
        await using var db = TestDb.NewContext();
        const string raw = "super-secret-raw-token";
        var inv = OnboardingTestData.Invitation(rawToken: raw);
        db.VendorOnboardingInvitations.Add(inv);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var loaded = await db.VendorOnboardingInvitations.FirstAsync(x => x.Id == inv.Id);
        loaded.TokenHash.Should().Be(VendorOnboardingInvitation.HashToken(raw));
        loaded.TokenHash.Should().NotContain(raw);
        loaded.Matches(raw).Should().BeTrue();
    }

    [Fact]
    public async Task FormTemplates_FilterByPurpose()   // D1
    {
        await using var db = TestDb.NewContext();
        db.FormTemplates.AddRange(SourcingSeed.FormTemplates(T));      // 2 RFQ forms
        db.FormTemplates.AddRange(OnboardingSeed.FormTemplates(T));    // 4 onboarding packs
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var onboarding = await db.FormTemplates.Where(f => f.Purpose == FormPurpose.Onboarding).ToListAsync();
        onboarding.Should().HaveCount(4);
        onboarding.Select(f => f.Name).Should().Contain(n => n.StartsWith("Health, Safety & Environment"));
        (await db.FormTemplates.CountAsync(f => f.Purpose == FormPurpose.Rfq)).Should().Be(2);
    }

    [Fact]
    public async Task ClarificationRounds_Persist_AppendOnly()   // F4
    {
        await using var db = TestDb.NewContext();
        var app = OnboardingTestData.UnderReview();
        app.RequestClarification(ClarificationDirection.BuyerToVendor, "Please address the items.",
            "u_faridah", "Faridah Yusof",
            [new OnboardingClarificationItem("ISO 9001", "Attach a valid certificate.")], T);
        app.Resubmit(["Attached."], T);
        db.VendorOnboardingApplications.Add(app);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var round = await db.OnboardingClarificationRounds
            .Include(r => r.Items).FirstAsync(r => r.ApplicationId == app.Id);
        round.Status.Should().Be(ClarificationRoundStatus.Responded);
        round.RoundNo.Should().Be(1);
        round.Items.Should().ContainSingle();
        round.Items[0].Request.Should().Be("Attach a valid certificate.");   // request immutable
        round.Items[0].Response.Should().Be("Attached.");
    }

    [Fact]
    public async Task Transition_WritesTypedAuditEntry()   // audit seam (DATA-MODEL §1)
    {
        var ctx = TestContext.New();
        var app = OnboardingTestData.Application(withFinancials: true);
        app.MarkInProgress(T);
        var t = app.Submit(T);

        // A service turns the returned transition into a typed audit entry from one chokepoint.
        await ctx.Audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code, t.Action,
            t.From.ToString(), t.To.ToString(), t.Reason);

        var entry = await ctx.Db.AuditEntries.FirstAsync(a => a.EntityType == "VendorOnboardingApplication");
        entry.FromState.Should().Be("InProgress");
        entry.ToState.Should().Be("Submitted");
        entry.Action.Should().Be("Application submitted");
        entry.ActorName.Should().Be("Faridah Yusof");
    }
}
