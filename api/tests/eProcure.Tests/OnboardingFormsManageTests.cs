using eProcure.Application.Abstractions;
using eProcure.Application.Onboarding;
using eProcure.Application.Sourcing;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Email;
using eProcure.Infrastructure.Seed;
using eProcure.Infrastructure.Persistence;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice E — supplier admins manage Onboarding-purpose question packs on the existing Forms page
/// (EDGE-CASES D5). A pack created via the Forms engine appears in the invite selector, renders in the
/// vendor form, its answers persist as OnboardingAnswer, and editing it affects NEW invites only —
/// an already-answered application keeps its answers.
/// </summary>
public class OnboardingFormsManageTests
{
    private sealed class NoEmail : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Harness
    {
        private readonly string _db = $"forms-{Guid.NewGuid()}";
        private readonly IOptions<OnboardingOptions> _opt =
            Options.Create(new OnboardingOptions { PortalBaseUrl = "http://localhost:5173/", LinkExpiryDays = 14 });
        public readonly FakeClock Clock = new(new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc));
        public readonly FakeCurrentUser User = new("u_faridah", "Faridah Yusof");

        public AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_db).Options);

        public async Task<T> Forms<T>(Func<FormService, Task<T>> f)
        {
            await using var db = NewContext();
            return await f(new FormService(db, Clock, new CodeGenerator(db, Clock)));
        }
        public async Task<T> Onboard<T>(Func<OnboardingService, Task<T>> f)
        {
            await using var db = NewContext();
            var svc = new OnboardingService(db, Clock, new CodeGenerator(db, Clock),
                new AuditLogWriter(db, Clock, User), new OnboardingNotifier(new NoEmail(), _opt),
                User, new FileStore(db, Clock), _opt);
            return await f(svc);
        }
        public Task Onboard(Func<OnboardingService, Task> f) => Onboard(async s => { await f(s); return 0; });
    }

    // ---- Task 2: the seeded packs use the EXISTING FormItem engine (valid vocab) with the right content ----
    [Fact]
    public void SeededPacks_UseValidFormItemTypes_AndExpectedContent()
    {
        var packs = OnboardingSeed.FormTemplates(new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc));

        packs.Should().HaveCount(4);
        packs.Select(p => p.Name).Should()
            .Contain(n => n.StartsWith("Health, Safety")).And
            .Contain(n => n.StartsWith("Quality")).And
            .Contain(n => n.StartsWith("Capability")).And
            .Contain(n => n.StartsWith("Compliance"));

        // every item maps to a real FormItemVocab type/kind (no second question system)
        var items = packs.SelectMany(p => p.Items).ToList();
        items.Should().OnlyContain(i => FormItemVocab.IsValidType(i.Type) && FormItemVocab.IsValidKind(i.Kind));
        items.Select(i => i.Type).Distinct().Should().Contain(["yesno", "attachment", "list", "multi", "number", "long_text", "date", "short_text"]);

        var hse = packs.First(p => p.Name.StartsWith("Health, Safety"));
        hse.Items.Should().Contain(i => i.Type == "yesno" && i.Required);   // documented HSE policy?
        hse.Items.Should().Contain(i => i.Type == "attachment");            // upload HSE policy
    }

    private static SaveFormTemplateRequest Pack(string name, string label, string purpose = "Onboarding") =>
        new(name, [new FormItemDto("question", "technical", "Section", label, "yesno", true, "{}", "", 0)], ["Section"], [], purpose);

    [Fact]
    public async Task OnboardingPack_CreatedOnFormsPage_FlowsThroughInvite_Render_AndAnswers()
    {
        var h = new Harness();

        // 1. Supplier admin creates an Onboarding-purpose pack via the Forms engine.
        var pack = await h.Forms(s => s.CreateAsync(Pack("Site Safety", "Do you have an HSE policy?")));
        pack.Purpose.Should().Be("Onboarding");

        // A default RFQ form is NOT offered to onboarding.
        await h.Forms(s => s.CreateAsync(new SaveFormTemplateRequest("RFQ form", [], [], [])));   // Purpose defaults to Rfq

        // 2. It appears in the invite selector (live, published).
        var templates = await h.Onboard(s => s.ListOnboardingTemplatesAsync());
        templates.Should().ContainSingle(t => t.Id == pack.Id && t.Name == "Site Safety");

        // 3. Invite selecting it → open → the vendor form renders it (D3) with its items.
        var inv = await h.Onboard(s => s.CreateInvitationAsync(new SendOnboardingInvitationRequest("v@x.my", "SWEC", "Acme", [pack.Id])));
        var token = Uri.UnescapeDataString(inv.MagicLink.Split("?t=")[1].Split('#')[0]);
        await h.Onboard(s => s.ResolveTokenAsync(token));

        var draft = await h.Onboard(s => s.GetDraftAsync(token));
        draft.Packs.Should().ContainSingle(p => p.Id == pack.Id);
        draft.Packs[0].Items.Should().ContainSingle(i => i.Label == "Do you have an HSE policy?");

        // 4. Answers persist as OnboardingAnswer.
        await h.Onboard(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "REG-1", null, null, null, null,
            null, null, null, [new OnboardingAnswerDto(pack.Id, 0, "Yes")])));
        (await h.Onboard(s => s.GetDraftAsync(token))).Answers.Should().ContainSingle(a => a.FormTemplateId == pack.Id && a.Value == "Yes");
    }

    // ---- D5: editing a pack affects NEW invites only — an already-answered application is unchanged ----
    [Fact]
    public async Task EditingPack_AffectsNewInvitesOnly_NotAnsweredApplications()
    {
        var h = new Harness();
        var pack = await h.Forms(s => s.CreateAsync(Pack("Site Safety", "Original question?")));

        // An application answers the pack as it was.
        var inv = await h.Onboard(s => s.CreateInvitationAsync(new SendOnboardingInvitationRequest("v@x.my", "SWEC", "Acme", [pack.Id])));
        var token = Uri.UnescapeDataString(inv.MagicLink.Split("?t=")[1].Split('#')[0]);
        await h.Onboard(s => s.ResolveTokenAsync(token));
        await h.Onboard(s => s.SaveDraftAsync(new SaveOnboardingDraftRequest(token, "Acme", "REG-1", null, null, null, null,
            null, null, null, [new OnboardingAnswerDto(pack.Id, 0, "Yes")])));

        // The admin edits the pack (changes the question).
        await h.Forms(s => s.UpdateAsync(pack.Id, Pack("Site Safety", "Revised question?")));

        // The already-answered application keeps its stored answer (answers are decoupled from the template).
        var draft = await h.Onboard(s => s.GetDraftAsync(token));
        draft.Answers.Should().ContainSingle(a => a.FormTemplateId == pack.Id && a.Value == "Yes");

        // But a NEW invite reads the revised pack.
        var newTemplates = await h.Onboard(s => s.ListOnboardingTemplatesAsync());
        newTemplates.Single(t => t.Id == pack.Id).Name.Should().Be("Site Safety");
        var inv2 = await h.Onboard(s => s.CreateInvitationAsync(new SendOnboardingInvitationRequest("v2@x.my", "SWEC", "Beta", [pack.Id])));
        var token2 = Uri.UnescapeDataString(inv2.MagicLink.Split("?t=")[1].Split('#')[0]);
        await h.Onboard(s => s.ResolveTokenAsync(token2));
        (await h.Onboard(s => s.GetDraftAsync(token2))).Packs[0].Items.Should().ContainSingle(i => i.Label == "Revised question?");
    }
}
