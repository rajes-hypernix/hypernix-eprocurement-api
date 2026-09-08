using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>
/// Smoke coverage for the migrated Suppliers onboarding workflow through to a real
/// vendor Identity login (VendorId claim). Sourcing golden-path tests reuse the same helper.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class SuppliersOnboardingGoldenPathTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SuppliersOnboardingGoldenPathTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task OnboardSwecVendor_Should_PromoteVendor_And_IssueVendorIdClaim()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer);

        using var getVendor = await buyer.GetAsync(
            $"{TestConstants.SuppliersBasePath}/vendors/{vendor.VendorId}");
        await EprocureFlowHelper.EnsureSuccessAsync(getVendor, "Get vendor");
        var vendorDto = await getVendor.DeserializeAsync<VendorDto>();
        vendorDto.Id.ShouldBe(vendor.VendorId);
        vendorDto.Code.ShouldBe(vendor.VendorCode);
        vendorDto.Name.ShouldContain("Acme");

        using var listUsers = await buyer.GetAsync(
            $"{TestConstants.SuppliersBasePath}/vendors/{vendor.VendorId}/users");
        await EprocureFlowHelper.EnsureSuccessAsync(listUsers, "List vendor users");
        var users = await listUsers.DeserializeAsync<List<VendorUserDto>>();
        users.ShouldContain(u => u.Email.Equals(vendor.Email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OnboardNonSwecVendor_Should_PersistSubmitAndDecisionSnapshots_And_IssueVendorIdClaim()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"nonswec-{unique}@example.com";
        var companyName = $"NonSwec Co {unique}";
        var registrationNo = $"NS-{unique}";
        var years = EprocureFlowHelper.SampleNonSwecFinancialYears();

        using var invite = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/invitations",
            new { email, type = "NonSwec", selectedTemplateIds = (string[]?)null });
        await EprocureFlowHelper.EnsureSuccessAsync(invite, "Invite NonSwec");
        var invitation = await invite.DeserializeAsync<OnboardingInvitationDto>();
        var token = EprocureFlowHelper.ExtractMagicLinkToken(invitation.MagicLink);

        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        using var resolve = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/resolve", new { token });
        await EprocureFlowHelper.EnsureSuccessAsync(resolve, "Resolve");
        var application = await resolve.DeserializeAsync<OnboardingApplicationDto>();
        application.Type.ShouldBe("NonSwec");

        using var draft = await anonymous.PutAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft",
            new
            {
                token,
                name = companyName,
                registeredName = companyName,
                registrationNo,
                email,
                contactName = "Portal Contact",
                contactPhone = "+60123456789",
                region = "Central",
                state = "Selangor",
                city = "Shah Alam",
                countryCode = "MY",
                financialYears = years
            });
        await EprocureFlowHelper.EnsureSuccessAsync(draft, "Save Non-SWEC draft");

        using var getDraft = await anonymous.GetAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft?token={Uri.EscapeDataString(token)}");
        await EprocureFlowHelper.EnsureSuccessAsync(getDraft, "Get draft");
        var saved = await getDraft.DeserializeAsync<OnboardingDraftDto>();
        saved.FinancialYears.Count.ShouldBe(3);

        using var submit = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft/submit", new { token });
        await EprocureFlowHelper.EnsureSuccessAsync(submit, "Submit Non-SWEC draft");

        var afterSubmit = await LoadSnapshotsAsync(application.Id);
        afterSubmit.Count.ShouldBe(1);
        afterSubmit[0].Stage.ShouldBe(FinancialSnapshotStage.AtSubmit);
        afterSubmit[0].Score.ShouldBeGreaterThan(0);
        afterSubmit[0].WeightedZ.ShouldBeGreaterThan(0);

        using var start = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/start-review", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(start, "Start review");

        using var approve = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/approve", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(approve, "Approve Non-SWEC");
        var approved = await approve.DeserializeAsync<OnboardingApproveResultDto>();

        var afterDecision = await LoadSnapshotsAsync(application.Id);
        afterDecision.Count.ShouldBe(2);
        afterDecision.ShouldContain(s => s.Stage == FinancialSnapshotStage.AtSubmit);
        afterDecision.ShouldContain(s => s.Stage == FinancialSnapshotStage.AtDecision);

        await EprocureFlowHelper.SetPasswordAsync(_factory, email, EprocureFlowHelper.DefaultVendorPassword);
        var tokenResult = await _auth.GetTokenAsync(email, EprocureFlowHelper.DefaultVendorPassword);
        EprocureFlowHelper.ReadVendorIdClaim(tokenResult.AccessToken).ShouldBe(approved.VendorId);
    }

    [Fact]
    public async Task ListSwec_Should_ReturnSeededTaxonomy_When_Authorized()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        using var response = await client.GetAsync($"{TestConstants.SuppliersBasePath}/swec");
        await EprocureFlowHelper.EnsureSuccessAsync(response, "List SWEC");
        var categories = await response.DeserializeAsync<List<SwecCategoryDto>>();
        categories.Count.ShouldBeGreaterThan(0);
    }

    private async Task<IReadOnlyList<FinancialSnapshot>> LoadSnapshotsAsync(Guid applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenant = await tenantStore.GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);

        var db = scope.ServiceProvider.GetRequiredService<SuppliersDbContext>();
        var assessment = await db.VendorFinancialAssessments
            .AsNoTracking()
            .Include(a => a.Snapshots)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);
        assessment.ShouldNotBeNull("Non-SWEC submit should have persisted a financial assessment.");
        return [.. assessment!.Snapshots.OrderBy(s => s.AsOfUtc)];
    }
}
