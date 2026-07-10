using eProcure.Application.Abstractions;
using eProcure.Application.Onboarding;
using eProcure.Domain;
using eProcure.Domain.Onboarding;
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
/// Slice C — the vendor onboarding form (EDGE-CASES B1–B2, B6, D2–D3, A3; save/resume). Each service
/// call runs on a FRESH <see cref="AppDbContext"/> over one shared InMemory store — mirroring the
/// per-request scope the API uses in production, so the token-scoped draft/save/submit behave exactly
/// as they do live. Asserts the financial snapshot on submit matches the prototype model.
/// </summary>
public class OnboardingFormTests
{
    private sealed class NoEmail : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>A shared InMemory store; every operation gets a fresh context + service (a fresh scope).</summary>
    private sealed class Harness
    {
        private readonly string _db = $"onboarding-{Guid.NewGuid()}";
        private readonly IOptions<OnboardingOptions> _opt =
            Options.Create(new OnboardingOptions { PortalBaseUrl = "http://localhost:5173/", LinkExpiryDays = 14 });
        public readonly FakeClock Clock = new(new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc));
        public readonly FakeCurrentUser User = new("u_faridah", "Faridah Yusof");

        public AppDbContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_db).Options);

        public async Task<T> Do<T>(Func<OnboardingService, Task<T>> f)
        {
            await using var db = NewContext();
            var svc = new OnboardingService(db, Clock, new CodeGenerator(db, Clock),
                new AuditLogWriter(db, Clock, User), new OnboardingNotifier(new NoEmail(), _opt),
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

    private static async Task<(Harness H, string Token, Guid[] Packs)> InvitedAsync(string type)
    {
        var h = new Harness();
        await using (var seed = h.NewContext())
        {
            seed.FormTemplates.AddRange(OnboardingSeed.FormTemplates(h.Clock.UtcNow));
            await seed.SaveChangesAsync();
        }
        var packs = (await h.Do(s => s.ListOnboardingTemplatesAsync())).Take(2).Select(t => t.Id).ToArray();
        var dto = await h.Do(s => s.CreateInvitationAsync(new SendOnboardingInvitationRequest("v@x.my", type, "Acme Sdn Bhd", packs)));
        var token = Uri.UnescapeDataString(dto.MagicLink.Split("?t=")[1].Split('#')[0]);
        await h.Do(s => s.ResolveTokenAsync(token));               // Invited → InProgress
        return (h, token, packs);
    }

    // ---- D2/D3: the invited packs are attached and returned WITH their items to render ----
    [Fact]
    public async Task Draft_ReturnsSelectedPacks_WithItems()
    {
        var (h, token, packs) = await InvitedAsync("Non-SWEC");
        var draft = await h.Do(s => s.GetDraftAsync(token));

        draft.Packs.Should().HaveCount(2);
        draft.Packs.Select(p => p.Id).Should().BeEquivalentTo(packs);
        draft.Packs[0].Items.Should().NotBeEmpty();                // renderable form items (D3)
    }

    // ---- save/resume + D4: a saved draft round-trips (profile, banking, categories, answers) ----
    [Fact]
    public async Task SaveDraft_ThenGet_RoundTripsEverything()
    {
        var (h, token, packs) = await InvitedAsync("Non-SWEC");
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token,
            Name: "Sarawak Flow Controls", RegistrationNo: "1188221-P", Location: "Bintulu",
            Email: "sales@swflow.my", ContactName: "Aishah", ContactPhone: "082-000",
            Bank: new OnboardingBankDto("Maybank", "5141-2290", "MBBEMYKL"),
            Categories: ["E12", "E15"], Financials: FinC(),
            Answers: [new OnboardingAnswerDto(packs[0], 0, "Yes")])));

        var draft = await h.Do(s => s.GetDraftAsync(token));
        draft.Name.Should().Be("Sarawak Flow Controls");
        draft.RegistrationNo.Should().Be("1188221-P");
        draft.Bank.Bank.Should().Be("Maybank");
        draft.Categories.Should().BeEquivalentTo(["E12", "E15"]);
        draft.Financials.Should().HaveCount(3);
        draft.Answers.Should().ContainSingle(a => a.FormTemplateId == packs[0] && a.Value == "Yes");
        draft.FinancialBand.Should().Be("C");                      // live band from the saved figures
    }

    // ---- B2 + A3: Non-SWEC submit snapshots the financial assessment (band C, score 28) ----
    [Fact]
    public async Task Submit_NonSwec_CapturesFinancialSnapshot()
    {
        var (h, token, _) = await InvitedAsync("Non-SWEC");
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "REG-1", null, null, null, null, null, null, FinC(), null)));

        var draft = await h.Do(s => s.SubmitDraftAsync(token));
        draft.Status.Should().Be("Submitted");

        await using var db = h.NewContext();
        var app = await db.VendorOnboardingApplications.Include(a => a.Financial!).ThenInclude(f => f.Snapshots).FirstAsync();
        var snap = app.Financial!.Snapshots.Should().ContainSingle(s => s.Stage == FinancialSnapshotStage.AtSubmit).Subject;
        snap.Band.Should().Be(FinancialBand.C);
        snap.Score.Should().Be(28);
        snap.WeightedZ.Should().BeApproximately(1.258645, 1e-6);   // prototype parity
    }

    // ---- B1: SWEC skips financials (waiver) — submit works with no financial assessment ----
    [Fact]
    public async Task Submit_Swec_NeedsNoFinancials()
    {
        var (h, token, _) = await InvitedAsync("SWEC");
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "SWEC Vendor", "SWK-1", null, null, null, null, null, null, null, null)));

        var draft = await h.Do(s => s.SubmitDraftAsync(token));
        draft.Status.Should().Be("Submitted");

        await using var db = h.NewContext();
        (await db.VendorOnboardingApplications.FirstAsync()).Financial.Should().BeNull();   // no assessment
    }

    // ---- B1: financials sent for a SWEC application are ignored (waiver) ----
    [Fact]
    public async Task SaveDraft_Swec_IgnoresFinancials()
    {
        var (h, token, _) = await InvitedAsync("SWEC");
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "S", "R", null, null, null, null, null, null, FinC(), null)));

        (await h.Do(s => s.GetDraftAsync(token))).Financials.Should().BeEmpty();
    }

    // ---- B6: figures populated in one shot (as an import would) compute the same as the model ----
    [Fact]
    public async Task Submit_ImportedFigures_MatchManualCompute()
    {
        var (h, token, _) = await InvitedAsync("Non-SWEC");
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "REG-1", null, null, null, null, null, null, FinC(), null)));
        await h.Do(s => s.SubmitDraftAsync(token));

        await using var db = h.NewContext();
        var app = await db.VendorOnboardingApplications.Include(a => a.Financial!).ThenInclude(f => f.Years)
            .Include(a => a.Financial!).ThenInclude(f => f.Snapshots).FirstAsync();
        var expected = AltmanZModel.Weighted(app.Financial!.Years.OrderBy(y => y.YearIndex));
        app.Financial.Snapshots[0].WeightedZ.Should().BeApproximately(expected, 1e-9);
    }

    // ---- Non-SWEC submit is blocked until required core + financials are present ----
    [Fact]
    public async Task Submit_NonSwec_WithoutFinancials_IsRejected()
    {
        var (h, token, _) = await InvitedAsync("Non-SWEC");
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "REG-1", null, null, null, null, null, null, null, null)));

        var act = () => h.Do(s => s.SubmitDraftAsync(token));
        await act.Should().ThrowAsync<DomainRuleException>();
    }

    [Fact]
    public async Task Submit_MissingCoreFields_IsRejected()
    {
        var (h, token, _) = await InvitedAsync("SWEC");            // name/reg still blank
        var act = () => h.Do(s => s.SubmitDraftAsync(token));
        await act.Should().ThrowAsync<DomainRuleException>();
    }

    // ---- validation: financials must be exactly 3 distinct years indexed 0/1/2 ----
    [Fact]
    public async Task SaveDraft_BadFinancialYearShape_IsRejected()
    {
        var (h, token, _) = await InvitedAsync("Non-SWEC");
        var twoYears = FinC().Take(2).ToArray();
        var dupIndex = new[] { FinC()[0], FinC()[2], FinC()[2] };   // indices 0,2,2

        var badCount = () => h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "R", null, null, null, null, null, null, twoYears, null)));
        await badCount.Should().ThrowAsync<DomainRuleException>();
        var badDup = () => h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "R", null, null, null, null, null, null, dupIndex, null)));
        await badDup.Should().ThrowAsync<DomainRuleException>();
    }

    // ---- validation: Non-SWEC submit needs positive total assets/liabilities (no garbage snapshot) ----
    [Fact]
    public async Task Submit_ZeroTotalAssets_IsRejected()
    {
        var (h, token, _) = await InvitedAsync("Non-SWEC");
        var zeroAssets = new[]
        {
            new OnboardingFinancialYearDto(0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1),   // TotalAssets = 0
            new OnboardingFinancialYearDto(1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1),
            new OnboardingFinancialYearDto(2, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1),
        };
        await h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "R", null, null, null, null, null, null, zeroAssets, null)));

        var act = () => h.Do(s => s.SubmitDraftAsync(token));
        await act.Should().ThrowAsync<DomainRuleException>();
    }

    // ---- an out-of-range financial figure is rejected with a friendly error, not a 500 (numeric overflow) ----
    [Fact]
    public async Task SaveDraft_OutOfRangeFinancialFigure_IsRejectedFriendly_NotA500()
    {
        var (h, token, _) = await InvitedAsync("Non-SWEC");
        var huge = new[]
        {
            new OnboardingFinancialYearDto(0, 1, 1, 1, 1_500_000_000_000m, 1, 1, 1, 1, 1, 1, 1),   // TotalAssets past the RM'000 cap
            new OnboardingFinancialYearDto(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1),
            new OnboardingFinancialYearDto(2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1),
        };
        var act = () => h.Do(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "R", null, null, null, null, null, null, huge, null)));
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*out of range*");
    }

    // ---- documents: upload stores a StoredFile and references it on the application ----
    [Fact]
    public async Task UploadDocument_StoresFile_AndReferences()
    {
        var (h, token, _) = await InvitedAsync("Non-SWEC");
        var doc = await h.Do(s => s.UploadDocumentAsync(token, "ssm", "SSM.pdf", "application/pdf", [1, 2, 3]));
        doc.Key.Should().Be("ssm");

        await using var db = h.NewContext();
        (await db.StoredFiles.CountAsync()).Should().Be(1);
        (await h.Do(s => s.GetDraftAsync(token))).Documents.Should().ContainSingle(d => d.Key == "ssm" && d.FileName == "SSM.pdf");
    }
}
