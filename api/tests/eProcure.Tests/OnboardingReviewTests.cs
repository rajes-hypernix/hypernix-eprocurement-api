using eProcure.Application.Abstractions;
using eProcure.Application.Onboarding;
using eProcure.Application.Suppliers;
using eProcure.Domain;
using eProcure.Domain.Onboarding;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Email;
using eProcure.Infrastructure.Persistence;
using eProcure.Infrastructure.Seed;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice D — buyer review, batched clarification, approve/reject + promotion, and the full E2E
/// (EDGE-CASES A4–A8, C1–C2, E4, F3, G3). Each call runs on a fresh <see cref="AppDbContext"/> over
/// one shared store (the per-request scope), with a shared fake email sender to assert E4 messages.
/// </summary>
public class OnboardingReviewTests
{
    private sealed class CapturingEmail : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private sealed class Harness
    {
        private readonly string _db = $"onboarding-{Guid.NewGuid()}";
        private readonly IOptions<OnboardingOptions> _opt = Options.Create(new OnboardingOptions
        {
            PortalBaseUrl = "http://localhost:5173/", TestRecipientOverride = "vendor-invites@hypernix.test",
            LinkExpiryDays = 14, DefaultVendorEmail = "vendor-invites@hypernix.test",
        });
        public readonly CapturingEmail Email = new();
        public readonly FakeClock Clock = new(new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc));
        public readonly FakeCurrentUser User = new("u_faridah", "Faridah Yusof");

        public AppDbContext NewContext()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_db).Options);
            // D7 schema invariant (migration-guaranteed in production): the scheme-consulting
            // mint needs the numbering rows; seed once per named store.
            if (!db.NumberingSchemes.Any())
            {
                db.NumberingSchemes.AddRange(eProcure.Application.Forms.EntryFormSeed.ToSchemeEntities());
                db.SaveChanges();
            }
            return db;
        }

        public async Task<T> Do<T>(Func<OnboardingService, Task<T>> f)
        {
            await using var db = NewContext();
            var svc = new OnboardingService(db, Clock, new CodeGenerator(db, Clock),
                new AuditLogWriter(db, Clock, User), new OnboardingNotifier(Email, _opt),
                User, new FileStore(db, Clock), _opt);
            return await f(svc);
        }
        public Task Do(Func<OnboardingService, Task> f) => Do(async s => { await f(s); return 0; });
    }

    private static OnboardingFinancialYearDto[] FinC() =>
    [
        new(0, 18000, 600, 1100, 20000, 9000, 3500, 7500, 13000, 7000, 3000, 8500),
        new(1, 17000, 400, 800, 20500, 8800, 3800, 8200, 14000, 6500, 2700, 8800),
        new(2, 16500, 350, 750, 21000, 8600, 4000, 8800, 14800, 6200, 2400, 9000),
    ];

    private static async Task<Harness> SeededHarness()
    {
        var h = new Harness();
        await using var seed = h.NewContext();
        seed.FormTemplates.AddRange(OnboardingSeed.FormTemplates(h.Clock.UtcNow));
        await seed.SaveChangesAsync();
        return h;
    }

    // Drives one application to UnderReview in the given harness (so several can share one store).
    private static async Task<(string Token, Guid AppId)> UnderReviewInAsync(Harness h, string type = "Non-SWEC", string email = "sales@acme.my")
    {
        var packs = (await h.Do(s => s.ListOnboardingTemplatesAsync())).Take(2).Select(t => t.Id).ToArray();
        var inv = await h.Do(s => s.CreateInvitationAsync(new SendOnboardingInvitationRequest("v@x.my", type, "Acme Sdn Bhd", packs)));
        var token = Uri.UnescapeDataString(inv.MagicLink.Split("?t=")[1].Split('#')[0]);
        await h.Do(s => s.ResolveTokenAsync(token));
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme Sdn Bhd", "1188221-P", "Bintulu",
            email, "Aishah", "082", new OnboardingBankDto("Maybank", "5141", "MBBEMYKL"),
            ["E12"], type == "SWEC" ? null : FinC(), [new OnboardingAnswerDto(packs[0], 0, "Yes")])));
        await h.Do(s => s.SubmitDraftAsync(token));
        await h.Do(s => s.StartReviewAsync(inv.ApplicationId!.Value));           // A4
        return (token, inv.ApplicationId!.Value);
    }

    private static async Task<(Harness H, string Token, Guid AppId)> UnderReviewAsync(string type = "Non-SWEC")
    {
        var h = await SeededHarness();
        var (token, appId) = await UnderReviewInAsync(h, type);
        return (h, token, appId);
    }

    // ---- A4: submitted → under review ----
    [Fact]
    public async Task StartReview_MovesSubmittedToUnderReview()
    {
        var (h, _, appId) = await UnderReviewAsync();
        (await h.Do(s => s.GetApplicationAsync(appId))).Status.Should().Be("UnderReview");
    }

    // ---- A7 + E4: one batched round → one clarification email ----
    [Fact]
    public async Task RequestClarification_RaisesOneRound_AndEmailsTheVendor()
    {
        var (h, _, appId) = await UnderReviewAsync();
        var review = await h.Do(s => s.RequestClarificationAsync(appId, new RequestClarificationRequest(
            "Please address these.", [new("ISO 9001", "Attach a valid certificate."), new("FY2025 accounts", "Add the latest year.")])));

        review.Status.Should().Be("ClarificationRequested");
        review.Rounds.Should().ContainSingle();
        review.Rounds[0].Items.Should().HaveCount(2);
        h.Email.Sent.Should().ContainSingle(m => m.Subject.Contains("Clarification"));   // one email
        h.Email.Sent.Last().To.Should().Be("vendor-invites@hypernix.test");                     // override (E3)
    }

    // ---- A8: vendor sees only the flagged items, resubmits all → back under review ----
    [Fact]
    public async Task Resubmit_FillsTheOpenRound_ThenReturnsUnderReview()
    {
        var (h, token, appId) = await UnderReviewAsync();
        await h.Do(s => s.RequestClarificationAsync(appId, new RequestClarificationRequest(
            "x", [new("ISO 9001", "Attach cert."), new("Bank letter", "Provide dated letter.")])));

        await h.Do(s => s.ResubmitAsync(new ResubmitOnboardingRequest(token, ["Attached.", "Provided."])));
        await h.Do(s => s.StartReviewAsync(appId));                               // buyer re-reviews

        var review = await h.Do(s => s.GetApplicationAsync(appId));
        review.Status.Should().Be("UnderReview");
        review.Rounds[0].Status.Should().Be("Responded");
        review.Rounds[0].Items[0].Response.Should().Be("Attached.");
        review.Rounds[0].Items[1].Response.Should().Be("Provided.");
    }

    // ---- both directions (SPEC §4): the vendor can raise a round back to the buyer ----
    [Fact]
    public async Task Vendor_CanRaiseAClarificationRound()
    {
        var (h, token, appId) = await UnderReviewAsync();
        await h.Do(s => s.RaiseClarificationAsync(new RaiseClarificationRequest(token, "A question from us.",
            [new("Delivery terms", "Can we propose 45-day terms?")])));

        var review = await h.Do(s => s.GetApplicationAsync(appId));
        review.Rounds.Should().ContainSingle(r => r.Direction == "VendorToBuyer");
    }

    // ---- F3: no Vendor / VendorUser exists before approval ----
    [Fact]
    public async Task NoVendorOrUser_BeforeApproval()
    {
        var (h, _, _) = await UnderReviewAsync();
        await using var db = h.NewContext();
        (await db.Vendors.CountAsync()).Should().Be(0);
        (await db.VendorUsers.CountAsync()).Should().Be(0);
    }

    // ---- A5 + E4 + §8: approve promotes to master + provisions login + copies assessment ----
    [Fact]
    public async Task Approve_PromotesToMaster_ProvisionsLogin_CopiesAssessment()
    {
        var (h, _, appId) = await UnderReviewAsync("Non-SWEC");
        var result = await h.Do(s => s.ApproveAsync(appId));

        result.VendorCode.Should().StartWith("SWK-V-2026-");

        await using var db = h.NewContext();
        var vendor = await db.Vendors.Include(v => v.BankAccounts).FirstAsync();
        vendor.Status.Should().Be(VendorStatus.Provisional);                     // Non-SWEC → Provisional
        vendor.RegistrationNo.Should().Be("1188221-P");
        vendor.BankAccounts.Should().ContainSingle(b => b.Bank == "Maybank");     // banking copied

        (await db.VendorUsers.CountAsync(u => u.VendorId == vendor.Id)).Should().Be(1);   // login provisioned
        var assessment = await db.VendorFinancialAssessments.Include(a => a.Snapshots).FirstAsync();
        assessment.VendorId.Should().Be(vendor.Id);                              // assessment copied onto vendor
        assessment.Snapshots.Should().Contain(s => s.Stage == FinancialSnapshotStage.AtDecision);

        var app = await db.VendorOnboardingApplications.FirstAsync(a => a.Id == appId);
        app.Status.Should().Be(OnboardingStatus.Approved);
        app.PromotedVendorId.Should().Be(vendor.Id);
        (await db.VendorOnboardingInvitations.FirstAsync()).Status.Should().Be(OnboardingInvitationStatus.Completed);
        h.Email.Sent.Should().Contain(m => m.Subject.Contains("approved"));      // E4 approval email
    }

    [Fact]
    public async Task Approve_Swec_IsRegistered()
    {
        var (h, _, appId) = await UnderReviewAsync("SWEC");
        await h.Do(s => s.ApproveAsync(appId));
        await using var db = h.NewContext();
        (await db.Vendors.FirstAsync()).Status.Should().Be(VendorStatus.Registered);   // SWEC → Registered
    }

    // ---- A6 + E4: reject with a reason; master untouched ----
    [Fact]
    public async Task Reject_IsTerminal_EmailsReason_MasterUntouched()
    {
        var (h, _, appId) = await UnderReviewAsync();
        await h.Do(s => s.RejectAsync(appId, "Incomplete audited accounts."));

        await using var db = h.NewContext();
        (await db.VendorOnboardingApplications.FirstAsync()).Status.Should().Be(OnboardingStatus.Rejected);
        (await db.Vendors.CountAsync()).Should().Be(0);                          // master untouched
        h.Email.Sent.Should().Contain(m => m.Subject.Contains("application") && m.HtmlBody.Contains("Incomplete audited accounts."));
    }

    // ---- duplicate check surfaces at approve (§8) ----
    [Fact]
    public async Task Approve_WithDuplicateRegistration_SurfacesWarning()
    {
        var (h, _, appId) = await UnderReviewAsync();
        await using (var db = h.NewContext())
        {
            db.Vendors.Add(new Vendor { Code = "SWK-V-0001", Name = "Other", RegisteredName = "Other", RegistrationNo = "1188221-P", CreatedUtc = h.Clock.UtcNow, UpdatedUtc = h.Clock.UtcNow });
            await db.SaveChangesAsync();
        }
        var result = await h.Do(s => s.ApproveAsync(appId));
        result.DuplicateWarning.Should().NotBeNull();
    }

    // ---- C1/C2: manual entry → straight to master (Registered), no approval; duplicate warned ----
    [Fact]
    public async Task ManualEntry_WritesToMaster_Registered_NoApproval_WithDuplicateCheck()
    {
        var ctx = TestContext.New();
        ctx.User.Roles = ["Buyer"];   // Buyer sees full bank details (Slice F masking)
        var svc = new VendorService(ctx.Db, ctx.Clock, ctx.Codes, ctx.Audit, ctx.User);

        var first = await svc.CreateManualAsync(new CreateManualVendorRequest("KL Supplies", "1099282-K", "Non-SWEC", null, null, null));
        first.Vendor.Status.Should().Be("Registered");                          // C1 — registered straight away
        first.DuplicateWarning.Should().BeNull();
        (await ctx.Db.AuditEntries.AnyAsync(a => a.EntityType == "Vendor" && a.Action.Contains("manual"))).Should().BeTrue();

        var dup = await svc.CreateManualAsync(new CreateManualVendorRequest("Another", "1099282-K", "Non-SWEC", null, null, null));
        dup.DuplicateWarning.Should().NotBeNull();                              // C2 — duplicate reg. no. warned
    }

    // ---- F3 guard-first: a replayed/illegal approval must NOT write a second master vendor ----
    [Fact]
    public async Task Approve_Twice_DoesNotWriteASecondVendor()
    {
        var (h, _, appId) = await UnderReviewAsync("Non-SWEC");
        await h.Do(s => s.ApproveAsync(appId));                                  // first: legit

        var act = () => h.Do(s => s.ApproveAsync(appId));                        // second: app is Approved
        await act.Should().ThrowAsync<DomainRuleException>();

        await using var db = h.NewContext();
        (await db.Vendors.CountAsync()).Should().Be(1);                          // no pollution from the failed approve
        (await db.VendorUsers.CountAsync()).Should().Be(1);
    }

    // ---- promotion materialises the primary contact + registered address from the captured scalars ----
    [Fact]
    public async Task Approve_MaterialisesPrimaryContactAndAddress()
    {
        var (h, _, appId) = await UnderReviewAsync("Non-SWEC");
        await h.Do(s => s.ApproveAsync(appId));

        await using var db = h.NewContext();
        var vendor = await db.Vendors.Include(v => v.Contacts).Include(v => v.Addresses).FirstAsync();
        vendor.Contacts.Should().ContainSingle(c => c.Name == "Aishah" && c.IsPrimary);
        vendor.Addresses.Should().ContainSingle(a => a.City == "Bintulu" && a.IsPrimary);
    }

    // ---- Resubmit answers the BUYER's open round even when a vendor-raised round is also open ----
    [Fact]
    public async Task Resubmit_AnswersBuyerRound_NotAConcurrentVendorRound()
    {
        var (h, token, appId) = await UnderReviewAsync();
        await h.Do(s => s.RequestClarificationAsync(appId, new RequestClarificationRequest("Fix", [new("ISO 9001", "Attach cert.")])));
        await h.Do(s => s.RaiseClarificationAsync(new RaiseClarificationRequest(token, "Our question", [new("Delivery terms", "45-day terms?")])));

        await h.Do(s => s.ResubmitAsync(new ResubmitOnboardingRequest(token, ["Attached."])));

        var review = await h.Do(s => s.GetApplicationAsync(appId));
        var buyer = review.Rounds.Single(r => r.Direction == "BuyerToVendor");
        var vendor = review.Rounds.Single(r => r.Direction == "VendorToBuyer");
        buyer.Status.Should().Be("Responded");
        buyer.Items[0].Response.Should().Be("Attached.");
        vendor.Status.Should().Be("Open");                                      // vendor round untouched
    }

    // ---- two applications with the same contact email both approve (login-email collision handled) ----
    [Fact]
    public async Task Approve_TwoAppsSameEmail_BothPromote_WithDistinctLogins()
    {
        var h = await SeededHarness();
        var (_, app1) = await UnderReviewInAsync(h, "Non-SWEC", "same@acme.my");
        var (_, app2) = await UnderReviewInAsync(h, "Non-SWEC", "same@acme.my");

        await h.Do(s => s.ApproveAsync(app1));
        var act = () => h.Do(s => s.ApproveAsync(app2));                         // must NOT throw a unique-index 500
        await act.Should().NotThrowAsync();

        await using var db = h.NewContext();
        (await db.VendorUsers.CountAsync()).Should().Be(2);
        (await db.VendorUsers.AsNoTracking().Select(u => u.Email).Distinct().CountAsync()).Should().Be(2);  // distinct logins
    }

    // ---- G3: the full end-to-end headline flow ----
    [Fact]
    public async Task E2E_Invite_Fill_Submit_Clarify_Resubmit_Approve()
    {
        var h = new Harness();
        await using (var seed = h.NewContext())
        {
            seed.FormTemplates.AddRange(OnboardingSeed.FormTemplates(h.Clock.UtcNow));
            await seed.SaveChangesAsync();
        }
        var packs = (await h.Do(s => s.ListOnboardingTemplatesAsync())).Take(2).Select(t => t.Id).ToArray();

        // 1. invite (email to vendor-invites@hypernix.test via override)
        var inv = await h.Do(s => s.CreateInvitationAsync(new SendOnboardingInvitationRequest("realvendor@acme.my", "Non-SWEC", "Sarawak Flow Controls", packs)));
        var token = Uri.UnescapeDataString(inv.MagicLink.Split("?t=")[1].Split('#')[0]);
        h.Email.Sent.Last().To.Should().Be("vendor-invites@hypernix.test");
        var appId = inv.ApplicationId!.Value;

        // 2. open + fill (Non-SWEC, live financials) + submit
        await h.Do(s => s.ResolveTokenAsync(token));
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Sarawak Flow Controls", "1188221-P",
            "Bintulu", "sales@swflow.my", "Aishah", "082", new OnboardingBankDto("Maybank", "5141", "MBBEMYKL"),
            ["E12", "E15"], FinC(), [new OnboardingAnswerDto(packs[0], 0, "Yes")])));
        await h.Do(s => s.SubmitDraftAsync(token));

        // 3. review → clarify 2 items (ONE email)
        await h.Do(s => s.StartReviewAsync(appId));
        var emailsBefore = h.Email.Sent.Count;
        await h.Do(s => s.RequestClarificationAsync(appId, new RequestClarificationRequest(
            "A couple of items.", [new("ISO 9001", "Attach cert."), new("FY2025", "Add accounts.")])));
        (h.Email.Sent.Count - emailsBefore).Should().Be(1);                      // exactly one clarification email

        // 4. vendor resubmits both → back under review
        await h.Do(s => s.ResubmitAsync(new ResubmitOnboardingRequest(token, ["Attached.", "Added."])));
        await h.Do(s => s.StartReviewAsync(appId));

        // 5. approve → master + login + assessment + snapshots + audit
        var result = await h.Do(s => s.ApproveAsync(appId));

        await using var db = h.NewContext();
        var vendor = await db.Vendors.FirstAsync();
        vendor.Code.Should().Be(result.VendorCode);
        vendor.Status.Should().Be(VendorStatus.Provisional);
        (await db.VendorUsers.CountAsync(u => u.VendorId == vendor.Id)).Should().Be(1);

        var assessment = await db.VendorFinancialAssessments.Include(a => a.Snapshots).FirstAsync();
        assessment.VendorId.Should().Be(vendor.Id);
        assessment.Snapshots.Select(s => s.Stage).Should().Contain(FinancialSnapshotStage.AtSubmit).And.Contain(FinancialSnapshotStage.AtDecision);

        var app = await db.VendorOnboardingApplications.Include(a => a.Rounds).FirstAsync();
        app.Status.Should().Be(OnboardingStatus.Approved);
        app.Rounds.Should().ContainSingle(r => r.Status == ClarificationRoundStatus.Responded);   // round preserved (append-only)

        // audit trail across the lifecycle + every email routed to the test address
        (await db.AuditEntries.CountAsync(a => a.EntityType == "VendorOnboardingApplication")).Should().BeGreaterThanOrEqualTo(4);
        (await db.AuditEntries.AnyAsync(a => a.EntityType == "Vendor" && a.Action.Contains("promoted"))).Should().BeTrue();
        h.Email.Sent.Should().OnlyContain(m => m.To == "vendor-invites@hypernix.test");
        h.Email.Sent.Select(m => m.Subject).Should().Contain(s => s.Contains("onboarding"))       // invitation
            .And.Contain(s => s.Contains("Clarification")).And.Contain(s => s.Contains("approved"));
    }
}
