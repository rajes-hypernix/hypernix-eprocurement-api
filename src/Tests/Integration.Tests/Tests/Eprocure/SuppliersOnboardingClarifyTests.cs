using FSH.Framework.Mailing.Services;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>Suppliers onboarding clarify → vendor resubmit → buyer re-review → approve.</summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class SuppliersOnboardingClarifyTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SuppliersOnboardingClarifyTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task ClarifyThenResubmit_Should_AllowApproveAndVendorLogin()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"clarify-{unique}@example.com";

        using var invite = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/invitations",
            new { email, type = "Swec", selectedTemplateIds = (string[]?)null });
        await EprocureFlowHelper.EnsureSuccessAsync(invite, "Invite");
        var invitation = await invite.DeserializeAsync<OnboardingInvitationDto>();
        var token = EprocureFlowHelper.ExtractMagicLinkToken(invitation.MagicLink);

        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        using var resolve = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/resolve", new { token });
        await EprocureFlowHelper.EnsureSuccessAsync(resolve, "Resolve");
        var application = await resolve.DeserializeAsync<OnboardingApplicationDto>();

        using var draft = await anonymous.PutAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft",
            new
            {
                token,
                name = $"Clarify Co {unique}",
                registeredName = $"Clarify Co {unique}",
                registrationNo = $"CL-{unique}",
                email
            });
        await EprocureFlowHelper.EnsureSuccessAsync(draft, "Draft");

        using var submit = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft/submit", new { token });
        await EprocureFlowHelper.EnsureSuccessAsync(submit, "Submit");

        using var start = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/start-review", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(start, "Start review");

        var mail = (NoOpMailService)_factory.Services.GetRequiredService<IMailService>();
        mail.Clear();

        using var clarify = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/clarify",
            new
            {
                message = "Need SSM certificate scan",
                items = new[]
                {
                    new ClarificationItemInput("Documents", "Please upload a clear SSM certificate.")
                }
            });
        await EprocureFlowHelper.EnsureSuccessAsync(clarify, "Clarify");
        var clarified = await clarify.DeserializeAsync<OnboardingApplicationDto>();
        clarified.Status.ShouldBe("ClarificationRequested");

        // Clarify reissues the magic-link token — capture it from the outbound email.
        var clarificationMail = mail.Sent.LastOrDefault(m =>
            m.Subject.Contains("Clarification", StringComparison.OrdinalIgnoreCase));
        clarificationMail.ShouldNotBeNull("Expected a clarification email with a fresh magic link.");
        var resubmitToken = EprocureFlowHelper.ExtractMagicLinkToken(
            ExtractLinkFromBody(clarificationMail!.Body));

        using var resubmit = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft/resubmit",
            new { token = resubmitToken, responses = new[] { "Uploaded SSM certificate." } });
        await EprocureFlowHelper.EnsureSuccessAsync(resubmit, "Resubmit");
        var resubmitted = await resubmit.DeserializeAsync<OnboardingApplicationDto>();
        resubmitted.Status.ShouldBe("Resubmitted");

        using var startAgain = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/start-review", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(startAgain, "Start review again");

        using var approve = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/approve", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(approve, "Approve");
        var approved = await approve.DeserializeAsync<OnboardingApproveResultDto>();

        await EprocureFlowHelper.SetPasswordAsync(_factory, email, EprocureFlowHelper.DefaultVendorPassword);
        var tokenResult = await _auth.GetTokenAsync(email, EprocureFlowHelper.DefaultVendorPassword);
        EprocureFlowHelper.ReadVendorIdClaim(tokenResult.AccessToken).ShouldBe(approved.VendorId);
    }

    private static string ExtractLinkFromBody(string? body)
    {
        body.ShouldNotBeNullOrWhiteSpace();
        // Body ends with "... Please respond via: {link}"
        const string marker = "via: ";
        var idx = body!.LastIndexOf(marker, StringComparison.Ordinal);
        idx.ShouldBeGreaterThanOrEqualTo(0);
        return body[(idx + marker.Length)..].Trim();
    }
}
