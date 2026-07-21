using FSH.Modules.Suppliers.Domain.Onboarding;

namespace Suppliers.Tests.Domain;

public sealed class VendorOnboardingApplicationTests
{
    [Fact]
    public void Submit_Should_Throw_When_NameOrRegistrationMissing()
    {
        var app = StartSwecApp();

        Should.Throw<OnboardingRuleException>(() => app.Submit(DateTime.UtcNow));
    }

    [Fact]
    public void Submit_Then_Approve_Should_Promote_SwecHappyPath()
    {
        var app = StartSwecApp();
        SaveMinimalDraft(app);

        app.Submit(DateTime.UtcNow);
        app.Status.ShouldBe(OnboardingStatus.Submitted);

        app.StartReview(DateTime.UtcNow);
        app.Status.ShouldBe(OnboardingStatus.UnderReview);

        var vendorId = Guid.NewGuid();
        app.Approve(vendorId, "buyer-1", "Buyer", DateTime.UtcNow);

        app.Status.ShouldBe(OnboardingStatus.Approved);
        app.PromotedVendorId.ShouldBe(vendorId);
    }

    [Fact]
    public void Approve_Should_Throw_When_NotUnderReview()
    {
        var app = StartSwecApp();
        SaveMinimalDraft(app);
        app.Submit(DateTime.UtcNow);

        Should.Throw<OnboardingRuleException>(() =>
            app.Approve(Guid.NewGuid(), "buyer-1", "Buyer", DateTime.UtcNow));
    }

    [Fact]
    public void Reject_Should_Require_Reason_And_Terminalize()
    {
        var app = StartSwecApp();
        SaveMinimalDraft(app);
        app.Submit(DateTime.UtcNow);
        app.StartReview(DateTime.UtcNow);

        Should.Throw<OnboardingRuleException>(() =>
            app.Reject(" ", "buyer-1", "Buyer", DateTime.UtcNow));

        app.Reject("Incomplete docs", "buyer-1", "Buyer", DateTime.UtcNow);
        app.Status.ShouldBe(OnboardingStatus.Rejected);
        app.RejectReason.ShouldBe("Incomplete docs");
    }

    private static void SaveMinimalDraft(VendorOnboardingApplication app) =>
        app.SaveDraft(
            name: "Acme",
            registeredName: "Acme Sdn Bhd",
            registrationNo: "SSM-1",
            taxId: null,
            email: null,
            contactName: null,
            contactPhone: null,
            region: null,
            state: null,
            city: null,
            country: null,
            categories: null,
            nowUtc: DateTime.UtcNow);

    private static VendorOnboardingApplication StartSwecApp()
    {
        var now = DateTime.UtcNow;
        var invitation = VendorOnboardingInvitation.Create(
            "v@example.com",
            "Swec",
            [],
            rawToken: Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            invitedByUserId: "buyer-1",
            invitedByName: "Buyer",
            nowUtc: now);
        var app = VendorOnboardingApplication.CreateFromInvitation("VOB-1", invitation, now);
        invitation.AttachApplication(app.Id);
        app.MarkInProgress(now);
        return app;
    }
}
