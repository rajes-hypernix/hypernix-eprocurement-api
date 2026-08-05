using FSH.Framework.Mailing.Templates;

namespace Framework.Tests.Mailing;

public sealed class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer _sut = new();

    [Fact]
    public void Render_Should_SubstituteTokens_AndHtmlEncodeValues()
    {
        string html = _sut.Render(
            EmailTemplateNames.ResetPassword,
            new Dictionary<string, string?>
            {
                ["UserName"] = "Alex <Admin>",
                ["ActionUrl"] = "https://app.example/reset?token=abc&email=a%2Bb@x.com&tenant=root",
            });

        html.ShouldContain("Hi Alex &lt;Admin&gt;,");
        html.ShouldContain("Set new password");
        html.ShouldContain("https://app.example/reset?token=abc&amp;email=a%2Bb@x.com&amp;tenant=root");
        html.ShouldNotContain("{{UserName}}");
        html.ShouldNotContain("{{ActionUrl}}");
    }

    [Fact]
    public void Render_Should_Throw_When_TemplateMissing()
    {
        Should.Throw<FileNotFoundException>(() =>
            _sut.Render("does-not-exist", new Dictionary<string, string?>()));
    }

    [Theory]
    [InlineData(EmailTemplateNames.OnboardingInvite, "Start onboarding", "ActionUrl")]
    [InlineData(EmailTemplateNames.OnboardingClarification, "Respond to clarification", "ApplicationCode")]
    [InlineData(EmailTemplateNames.OnboardingApproved, "Onboarding approved", "VendorCode")]
    [InlineData(EmailTemplateNames.OnboardingRejected, "Onboarding decision", "Reason")]
    public void Render_Should_LoadOnboardingTemplates(string templateName, string expectedSnippet, string requiredToken)
    {
        string html = _sut.Render(
            templateName,
            new Dictionary<string, string?>
            {
                ["UserName"] = "Vendor",
                ["InvitedByName"] = "Buyer",
                ["ExpiresUtc"] = "2026-08-01 00:00:00Z",
                ["ActionUrl"] = "https://app.example/onboard?token=abc",
                ["ApplicationCode"] = "ONB-1",
                ["Message"] = "Please upload SSM",
                ["VendorCode"] = "V-100",
                ["Reason"] = "Incomplete documents",
            });

        html.ShouldContain(expectedSnippet);
        html.ShouldContain("Hypernix eProcure");
        html.ShouldNotContain($"{{{{{requiredToken}}}}}");
    }
}