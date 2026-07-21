using FSH.Modules.Platform.Contracts;
using FSH.Modules.Platform.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>Smoke: Platform lookup seed + authenticated list endpoints.</summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class PlatformLookupsSmokeTests
{
    private readonly AuthHelper _auth;

    public PlatformLookupsSmokeTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task SeededLookups_Should_BeReadableByBuyer()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();

        using var countriesRes = await buyer.GetAsync($"{TestConstants.PlatformBasePath}/countries");
        await EprocureFlowHelper.EnsureSuccessAsync(countriesRes, "List countries");
        var countries = await countriesRes.DeserializeAsync<List<CountryDto>>();
        countries.ShouldContain(c => c.Code == "MY");

        var my = countries.First(c => c.Code == "MY");
        using var statesRes = await buyer.GetAsync($"{TestConstants.PlatformBasePath}/countries/{my.Id}/states");
        await EprocureFlowHelper.EnsureSuccessAsync(statesRes, "List states");
        var states = await statesRes.DeserializeAsync<List<StateDto>>();
        states.Count.ShouldBeGreaterThan(0);

        using var banksRes = await buyer.GetAsync($"{TestConstants.PlatformBasePath}/banks?countryCode=MY");
        await EprocureFlowHelper.EnsureSuccessAsync(banksRes, "List banks");
        var banks = await banksRes.DeserializeAsync<List<BankDto>>();
        banks.Count.ShouldBeGreaterThanOrEqualTo(5);

        using var itemsRes = await buyer.GetAsync(
            $"{TestConstants.PlatformBasePath}/custom-lists/{CustomListKeys.RfqRescind}/items");
        await EprocureFlowHelper.EnsureSuccessAsync(itemsRes, "List rescind reasons");
        var items = await itemsRes.DeserializeAsync<List<CustomListItemDto>>();
        items.ShouldContain(i => i.Code == "OTHER");

        using var orgRes = await buyer.GetAsync($"{TestConstants.PlatformBasePath}/org-catalog");
        await EprocureFlowHelper.EnsureSuccessAsync(orgRes, "Org catalog");
        var org = await orgRes.DeserializeAsync<OrgCatalogDto>();
        org.Units.ShouldContain(u => u.Code == "PROC" && u.Type == "Department");

        using var templatesRes = await buyer.GetAsync($"{TestConstants.PlatformBasePath}/form-templates");
        await EprocureFlowHelper.EnsureSuccessAsync(templatesRes, "Form templates");
        var templates = await templatesRes.DeserializeAsync<List<FormTemplateListItemDto>>();
        templates.ShouldContain(t => t.Key == FormTemplateKeys.OnboardingSwec);
    }
}
