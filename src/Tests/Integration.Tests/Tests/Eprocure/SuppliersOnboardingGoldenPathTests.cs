using FSH.Modules.Suppliers.Contracts.Dtos;
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
    public async Task ListSwec_Should_ReturnSeededTaxonomy_When_Authorized()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        using var response = await client.GetAsync($"{TestConstants.SuppliersBasePath}/swec");
        await EprocureFlowHelper.EnsureSuccessAsync(response, "List SWEC");
        var categories = await response.DeserializeAsync<List<SwecCategoryDto>>();
        categories.Count.ShouldBeGreaterThan(0);
    }
}
