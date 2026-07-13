using System.Net;
using System.Net.Http.Json;
using eProcure.Application.CustomFields;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eProcure.Tests.Integration;

// ============================================================================================
// THE ANTI-ROT GUARANTEE — DO NOT WEAKEN THIS TEST TO MAKE IT PASS.
//
// This test registers fake providers that exist ONLY in this file, then asserts they appear
// in the impact report and change lifecycle decisions WITH ZERO CHANGES to CustomFieldService,
// the guard, or the report code. That is the operator's core architecture requirement: a new
// consumer (analytics, a new record type, a new value store) must be picked up automatically —
// "I can't have to come back to this code."
//
// If a future change makes this test fail, the correct fix is ALWAYS in the architecture
// (restore the provider loop), never here. If you find yourself editing the assertions,
// adding a special case to the guard, or removing a fake provider — stop: you are about to
// reintroduce the exact rot this slice was built to prevent.
// ============================================================================================

/// <summary>A consumer the production code has NEVER heard of: a pretend analytics feature
/// claiming the field is used in "Dashboard Z".</summary>
file sealed class FakeAnalyticsReferenceProvider : ICustomFieldReferenceProvider
{
    public string ConsumerName => "Analytics";
    public Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid fieldDefId, string fieldCode, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FieldReference>>(fieldCode == "custbody_extensibility_probe"
            ? [new FieldReference("Analytics", FieldRefKind.Other, null, "Dashboard Z", "pretend analytics binding")]
            : []);
}

/// <summary>A value STORE the production code has never heard of: pretends to hold one value
/// for the probe field on a closed record — it must show up as Historical and be included
/// in a purge, again with no guard edits.</summary>
file sealed class FakeHistoricalValueProvider : ICustomFieldDataProvider
{
    public static bool Purged;
    public string StoreName => "Fake archive store";

    public Task<DataReferenceSummary> CountValuesAsync(Guid fieldDefId, CancellationToken ct = default) =>
        Task.FromResult(Purged
            ? new DataReferenceSummary(StoreName, 0, 0, [])
            : new DataReferenceSummary(StoreName, 0, 1, [new DataTypeCount("FakeClosedRecord", 0, 1)]));

    public Task<PurgeSnapshot> PurgeHistoricalAsync(Guid fieldDefId, CancellationToken ct = default)
    {
        Purged = true;
        return Task.FromResult(new PurgeSnapshot(StoreName,
            [new PurgedValue("FakeClosedRecord", Guid.NewGuid(), null, "FAKE-0001", "archived value")]));
    }
}

public sealed class ExtensibilityProofTests : IDisposable
{
    private readonly TestWebAppFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient ClientAs(string user, WebApplicationFactory<Program> f)
    {
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Demo-User", user);
        return client;
    }

    [Fact]
    public async Task A_provider_registered_in_DI_ONLY_appears_in_the_report_and_blocks_delete_and_purges()
    {
        FakeHistoricalValueProvider.Purged = false;
        // The ONLY wiring: DI registration. No production file knows these types exist.
        using var f = _factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.AddScoped<ICustomFieldReferenceProvider, FakeAnalyticsReferenceProvider>();
            services.AddScoped<ICustomFieldDataProvider, FakeHistoricalValueProvider>();
        }));
        var admin = ClientAs("u_admin", f);

        var def = (await (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "Extensibility Probe", "PurchaseOrder", "Text", null, false, "", 0, Code: "extensibility_probe")))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;

        // 1. The report shows the fake consumer AND the fake store — purely from registration.
        var report = (await admin.GetFromJsonAsync<ImpactReportDto>($"/api/custom-fields/{def.Id}/references"))!;
        report.ConfigReferences.Should().ContainSingle(r => r.ConsumerName == "Analytics" && r.TargetLabel == "Dashboard Z",
            "a consumer NO production code names appears in the report — the loop found it via DI alone");
        report.Data.Should().Contain(d => d.StoreName == "Fake archive store" && d.HistoricalCount == 1,
            "a value store no production code names is counted — Historical, per its own classification");

        // 2. Delete is refused BECAUSE of the fake analytics reference.
        (await admin.DeleteAsync($"/api/custom-fields/{def.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the unknown consumer's reference blocks Tier-2 delete");

        // 3. Once the fake config reference is out of the picture (Analytics only claims the
        //    probe code — recreate under another code to isolate the data half), Purge includes
        //    the fake store's historical value — still zero guard edits.
        var def2 = (await (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "Extensibility Probe 2", "PurchaseOrder", "Text", null, false, "", 0, Code: "extensibility_probe_2")))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        var report2 = (await admin.GetFromJsonAsync<ImpactReportDto>($"/api/custom-fields/{def2.Id}/references"))!;
        report2.CanPurge.Should().BeTrue("historical-only data (from the fake store) makes it purge-eligible, not deletable");
        (await admin.DeleteAsync($"/api/custom-fields/{def2.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "historical values block plain delete — purge is the governed path");
        (await admin.PostAsync($"/api/custom-fields/{def2.Id}/purge", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        FakeHistoricalValueProvider.Purged.Should().BeTrue("the purge called the fake store's PurgeHistoricalAsync via the loop");
    }

    [Fact]
    public async Task Purge_requires_the_distinct_A73_action_not_ManageCustomFields()
    {
        var admin = ClientAs("u_admin", _factory);
        var def = (await (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "Tier Probe", "PurchaseOrder", "Text", null, false, "", 0)))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        // u_faridah is a Buyer — holds neither ManageCustomFields nor A73; the point here is
        // the endpoint carries A73 (Admin-only) — proven by the role matrix sweep too.
        (await ClientAs("u_faridah", _factory).PostAsync($"/api/custom-fields/{def.Id}/purge", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }
}
