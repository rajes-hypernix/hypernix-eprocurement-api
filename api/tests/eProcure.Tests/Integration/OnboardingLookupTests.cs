using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eProcure.Domain.Configuration;
using eProcure.Domain.Onboarding;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// A2F-T2 (Obs-6/GAP-6): the anonymous magic-link form gets its reference data through
/// POST /api/onboarding/lookups — authorized by a LIVE invitation token, never a principal.
/// Pins: valid token → 200 with EXACTLY the form's needs (the four lists + SWEC, nothing
/// more — the strict-shape ruling); garbage token → 404; revoked and expired tokens →
/// refused. The general /custom-lists and /swec endpoints stay authenticated (the sweep
/// tests keep pinning that perimeter).
/// </summary>
public sealed class OnboardingLookupFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();
    public const string LiveToken = "a2f-live-token";
    public const string RevokedToken = "a2f-revoked-token";
    public const string ExpiredToken = "a2f-expired-token";

    public async Task InitializeAsync()
    {
        var now = DateTime.UtcNow;
        await Factory.SeedAsync(db =>
        {
            VendorOnboardingInvitation Inv(string raw, DateTime created) =>
                VendorOnboardingInvitation.Create("applicant@vendor.test", VendorType.NonSwec, [], raw, "u_faridah", "Faridah", created, validityDays: 14);

            var live = Inv(LiveToken, now);
            var revoked = Inv(RevokedToken, now);
            revoked.Revoke(now);
            // Created 60 days ago with 14-day validity → expired against the host's REAL clock.
            var expired = Inv(ExpiredToken, now.AddDays(-60));
            db.VendorOnboardingInvitations.AddRange(live, revoked, expired);

            foreach (var inv in new[] { live, revoked, expired })
            {
                var app = VendorOnboardingApplication.CreateFromInvitation($"VOB-2026-9{Array.IndexOf(new[] { live, revoked, expired }, inv)}01", inv, now);
                inv.AttachApplication(app.Id);
                db.VendorOnboardingApplications.Add(app);
            }

            var country = new CustomList { Code = "COUNTRY", Name = "Country", IsSystem = true };
            country.Values.Add(new CustomListValue { Code = "MY", Label = "Malaysia", Sort = 0 });
            var bank = new CustomList { Code = "BANK", Name = "Bank", IsSystem = true };
            bank.Values.Add(new CustomListValue { Code = "MBB", Label = "Maybank", Sort = 0 });
            var uom = new CustomList { Code = "UOM", Name = "Unit of measure", IsSystem = true };   // NOT a form list — must not leak
            uom.Values.Add(new CustomListValue { Code = "EA", Label = "Each", Sort = 0 });
            db.CustomLists.AddRange(country, bank, uom);

            db.SwecCategories.Add(new SwecCategory("40101800P", "Pumps", null, 1, true, "Pumps"));
            return Task.CompletedTask;
        });
    }

    public Task DisposeAsync() { Factory.Dispose(); return Task.CompletedTask; }

    public HttpClient Client() =>
        Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static Task<HttpResponseMessage> Lookups(HttpClient c, string token) =>
        c.PostAsJsonAsync("/api/onboarding/lookups", new { token });
}

public sealed class OnboardingLookupTests(OnboardingLookupFixture fx) : IClassFixture<OnboardingLookupFixture>
{
    [Fact]
    public async Task Valid_token_gets_exactly_the_forms_lookups_and_nothing_more()
    {
        var resp = await OnboardingLookupFixture.Lookups(fx.Client(), OnboardingLookupFixture.LiveToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var payload = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

        // STRICT SHAPE (ruled): the payload carries swec + customLists and nothing else.
        payload.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["swec", "customLists"]);
        payload.GetProperty("swec").EnumerateArray().Should().HaveCount(1);
        var listCodes = payload.GetProperty("customLists").EnumerateArray()
            .Select(l => l.GetProperty("code").GetString()).ToList();
        listCodes.Should().BeEquivalentTo(["COUNTRY", "BANK"], "only the form's lists that exist are served — UOM must NOT leak through the anonymous surface");
    }

    [Fact]
    public async Task Garbage_revoked_and_expired_tokens_are_refused()
    {
        (await OnboardingLookupFixture.Lookups(fx.Client(), "not-a-real-token")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "unknown token — existence-hiding, same as /resolve");
        (await OnboardingLookupFixture.Lookups(fx.Client(), OnboardingLookupFixture.RevokedToken)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "revoked invitation (DomainRuleException posture, same as /resolve)");
        (await OnboardingLookupFixture.Lookups(fx.Client(), OnboardingLookupFixture.ExpiredToken)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "expired invitation — same refusal as the resolve path");
    }

    [Fact]
    public async Task Anonymous_callers_still_cannot_reach_the_general_reference_endpoints()
    {
        // The whole point of the token gate: /custom-lists and /swec stay authenticated.
        (await fx.Client().GetAsync("/api/custom-lists")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fx.Client().GetAsync("/api/swec")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
