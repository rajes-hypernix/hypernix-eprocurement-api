using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Domain;
using eProcure.Application.Onboarding;
using eProcure.Domain.Onboarding;
using eProcure.Infrastructure.Email;
using eProcure.Infrastructure.Seed;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice B — invitation + email + magic link (EDGE-CASES E1–E5, A1–A2, A9–A10, F1–F2). Uses a fake
/// <see cref="IEmailSender"/> to assert recipient/subject/link, the real <see cref="OnboardingNotifier"/>
/// so the override + link-building are exercised, and the EF InMemory store for persistence.
/// </summary>
public class OnboardingInvitationTests
{
    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public EmailMessage Last => Sent[^1];
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static (OnboardingService Svc, FakeEmailSender Email, TestContext Ctx) NewService(
        string? testRecipientOverride = "vendor-invites@hypernix.test")
    {
        var ctx = TestContext.New();
        ctx.Db.FormTemplates.AddRange(OnboardingSeed.FormTemplates(ctx.Clock.UtcNow));
        ctx.Db.SaveChanges();

        var email = new FakeEmailSender();
        var opt = Options.Create(new OnboardingOptions
        {
            PortalBaseUrl = "http://localhost:5173/",
            TestRecipientOverride = testRecipientOverride,
            LinkExpiryDays = 14,
            DefaultVendorEmail = "vendor-invites@hypernix.test",
        });
        var notifier = new OnboardingNotifier(email, opt);
        var fileStore = new FileStore(ctx.Db, ctx.Clock);
        var svc = new OnboardingService(ctx.Db, ctx.Clock, ctx.Codes, ctx.Audit, notifier, ctx.User, fileStore, opt);
        return (svc, email, ctx);
    }

    private static async Task<Guid[]> TemplateIdsAsync(TestContext ctx, int take = 2) =>
        [.. (await ctx.Db.FormTemplates.Where(f => f.Purpose == Domain.Sourcing.FormPurpose.Onboarding)
            .OrderBy(f => f.Code).Take(take).ToListAsync()).Select(f => f.Id)];

    private static string TokenFrom(string magicLink)
    {
        var t = magicLink.Split("?t=")[1];
        return Uri.UnescapeDataString(t.Split('#')[0]);
    }

    // ---- A1: invite creates an Invited application with a VOB code + hashed token + audit ----
    [Fact]
    public async Task CreateInvitation_CreatesInvitedApplication_WithVobCode_HashedToken_AndAudit()
    {
        var (svc, email, ctx) = NewService();
        var packs = await TemplateIdsAsync(ctx);

        var dto = await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest(
            "buyer@vendor.my", "Non-SWEC", "Pacific Valve Sdn Bhd", packs));

        dto.Status.Should().Be("Sent");
        dto.ApplicationCode.Should().MatchRegex(@"^VOB-2026-\d{4}$");         // A1 — sequence code
        dto.MagicLink.Should().Contain("?t=").And.Contain("#onboard");

        var app = await ctx.Db.VendorOnboardingApplications.FirstAsync();
        app.Status.Should().Be(OnboardingStatus.Invited);
        app.Name.Should().Be("Pacific Valve Sdn Bhd");

        var inv = await ctx.Db.VendorOnboardingInvitations.FirstAsync();
        inv.TokenHash.Should().HaveLength(64);                                // F1 — stored hashed
        email.Sent.Should().ContainSingle();                                 // E1 — an email went out
        (await ctx.Db.AuditEntries.CountAsync(a => a.EntityType.StartsWith("VendorOnboarding"))).Should().Be(2);
    }

    // ---- E1: the email carries the magic link, and that link resolves to the application ----
    [Fact]
    public async Task InviteEmail_CarriesMagicLink_ThatResolves()
    {
        var (svc, email, _) = NewService();
        var dto = await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest(
            "buyer@vendor.my", "Non-SWEC", null, []));

        email.Last.Subject.Should().Contain("onboarding");
        email.Last.HtmlBody.Should().Contain(dto.MagicLink);                  // link is in the email

        var app = await svc.ResolveTokenAsync(TokenFrom(dto.MagicLink));      // link resolves
        app.Code.Should().Be(dto.ApplicationCode);
    }

    // ---- E2: server defaults a blank vendor email to vendor-invites@hypernix.test ----
    [Fact]
    public async Task CreateInvitation_BlankEmail_DefaultsToTestAddress()
    {
        var (svc, _, ctx) = NewService(testRecipientOverride: null);
        await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest(null, "Non-SWEC", null, []));

        (await ctx.Db.VendorOnboardingApplications.FirstAsync()).Email.Should().Be("vendor-invites@hypernix.test");
    }

    // ---- E3: the override routes ALL onboarding email to the test address; without it, the real one ----
    [Fact]
    public async Task Override_RoutesEmailToTestAddress()
    {
        var (svc, email, _) = NewService(testRecipientOverride: "vendor-invites@hypernix.test");
        await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest("realvendor@acme.my", "Non-SWEC", null, []));
        email.Last.To.Should().Be("vendor-invites@hypernix.test");
    }

    [Fact]
    public async Task NoOverride_EmailGoesToTheRealVendor()
    {
        var (svc, email, _) = NewService(testRecipientOverride: null);
        await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest("realvendor@acme.my", "Non-SWEC", null, []));
        email.Last.To.Should().Be("realvendor@acme.my");
    }

    // ---- E5: no secrets in code — the SMTP transport ships with empty credentials (config-only) ----
    [Fact]
    public void SmtpOptions_HaveNoHardcodedSecrets()
    {
        var o = new EmailOptions();
        o.Smtp.Host.Should().BeEmpty();
        o.Smtp.Username.Should().BeEmpty();
        o.Smtp.Password.Should().BeEmpty();
    }

    // ---- A2 / F2: resolving a token opens ITS application (InProgress) and scopes to it ----
    [Fact]
    public async Task ResolveToken_OpensOwnApplication_Only()
    {
        var (svc, _, ctx) = NewService();
        var a = await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest("a@x.my", "Non-SWEC", "Alpha", []));
        var b = await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest("b@x.my", "SWEC", "Bravo", []));

        var resolvedA = await svc.ResolveTokenAsync(TokenFrom(a.MagicLink));
        resolvedA.Code.Should().Be(a.ApplicationCode);                       // A2 — its own app
        resolvedA.Status.Should().Be("InProgress");                          // Invited → InProgress
        resolvedA.Code.Should().NotBe(b.ApplicationCode);                    // F2 — scoped, not the other

        // A wrong token resolves to nothing.
        var bad = () => svc.ResolveTokenAsync("not-a-real-token");
        await bad.Should().ThrowAsync<NotFoundException>();
    }

    // ---- A9: an expired link does not resolve; a resend issues a new token that does ----
    [Fact]
    public async Task ExpiredLink_DoesNotResolve_ButResendIssuesAWorkingOne()
    {
        var (svc, _, ctx) = NewService();
        var dto = await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest("v@x.my", "Non-SWEC", null, []));

        ctx.Clock.UtcNow = ctx.Clock.UtcNow.AddDays(15);                     // past the 14-day expiry
        var expired = () => svc.ResolveTokenAsync(TokenFrom(dto.MagicLink));
        await expired.Should().ThrowAsync<DomainRuleException>();

        var resent = await svc.ResendInvitationAsync(dto.Id);
        var app = await svc.ResolveTokenAsync(TokenFrom(resent.MagicLink));  // the new token works
        app.Status.Should().Be("InProgress");
    }

    // ---- A10: a revoked invite's token no longer resolves ----
    [Fact]
    public async Task RevokedInvite_TokenNoLongerResolves()
    {
        var (svc, _, ctx) = NewService();
        var dto = await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest("v@x.my", "Non-SWEC", null, []));

        await svc.RevokeInvitationAsync(dto.Id);

        (await ctx.Db.VendorOnboardingInvitations.FirstAsync()).Status.Should().Be(OnboardingInvitationStatus.Revoked);
        var act = () => svc.ResolveTokenAsync(TokenFrom(dto.MagicLink));
        await act.Should().ThrowAsync<DomainRuleException>();
    }

    // ---- a revoked invitation is a firm withdrawal — it cannot be resent ----
    [Fact]
    public async Task Resend_AfterRevoke_IsRejected()
    {
        var (svc, _, _) = NewService();
        var dto = await svc.CreateInvitationAsync(new SendOnboardingInvitationRequest("v@x.my", "Non-SWEC", null, []));
        await svc.RevokeInvitationAsync(dto.Id);

        var act = () => svc.ResendInvitationAsync(dto.Id);
        await act.Should().ThrowAsync<DomainRuleException>();
    }

    // ---- D1 (Slice B usage): only onboarding-purpose templates are offered at invite ----
    [Fact]
    public async Task ListOnboardingTemplates_ReturnsOnlyOnboardingPacks()
    {
        var (svc, _, ctx) = NewService();
        ctx.Db.FormTemplates.AddRange(SourcingSeed.FormTemplates(ctx.Clock.UtcNow));   // add the RFQ forms too
        await ctx.Db.SaveChangesAsync();

        var templates = await svc.ListOnboardingTemplatesAsync();
        templates.Should().HaveCount(4);
        templates.Should().OnlyContain(t => t.QuestionCount > 0);
    }
}
