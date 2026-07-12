using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace eProcure.Tests.Integration;

/// <summary>
/// The perimeter's real guarantee: EVERY registered endpoint requires authentication (401 when
/// anonymous) EXCEPT an exactly-enumerated exemption list. A new endpoint added without thought is
/// closed by default and will fail this test until someone deliberately lists it. The burden of
/// proof is on ADDING an exemption, never on removing one.
/// </summary>
public sealed class AnonymousSweepTests(ITestOutputHelper output)
{
    // The designed [AllowAnonymous] set (method + ApiExplorer relative path). Anything else must 401.
    private static readonly HashSet<string> Exemptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET api/health",                                    // liveness probe (no principal, no data)
        "GET api/auth/dev-users",                            // demo bootstrap (internally demo-gated)
        "POST api/auth/dev-login",                           // demo bootstrap (internally demo-gated)
        "POST api/onboarding/resolve",                       // magic-link: token IS the scope (F2)
        "POST api/onboarding/lookups",                       // A2F-T2 (Obs-6): form reference data; token-gated in the service (revoked/expired refused), payload capped to the 4 lists + SWEC
        "GET api/onboarding/draft",                          // token-scoped, no login
        "PUT api/onboarding/draft",                          // token-scoped, no login
        "POST api/onboarding/draft/submit",                  // token-scoped, no login
        "POST api/onboarding/draft/documents",               // token-scoped, no login
        "DELETE api/onboarding/draft/documents/{key}",       // token-scoped, no login
        "POST api/onboarding/draft/resubmit",                // token-scoped, no login
        "POST api/onboarding/draft/raise-clarification",     // token-scoped, no login
    };

    [Fact]
    public async Task Every_endpoint_requires_auth_except_the_exact_exemption_list()
    {
        await using var factory = new TestWebAppFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var provider = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

        var endpoints = provider.ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Where(d => d.HttpMethod is not null)
            .Select(d => (Method: d.HttpMethod!, Template: d.RelativePath ?? ""))
            .Distinct()
            .OrderBy(e => e.Template).ThenBy(e => e.Method)
            .ToList();

        endpoints.Should().NotBeEmpty("the API must expose endpoints to sweep");

        var reachedAnonymously = new List<string>();   // endpoints that did NOT return 401
        var leaks = new List<string>();                 // protected endpoints that let an anon through
        var mislabeledExemptions = new List<string>();  // exemptions that 401'd

        output.WriteLine($"{"STATUS",-8} {"METHOD",-7} PATH");
        foreach (var (method, template) in endpoints)
        {
            var path = "/" + Regex.Replace(template, "\\{[^}]+\\}", "00000000-0000-0000-0000-000000000001");
            using var req = new HttpRequestMessage(new HttpMethod(method), path);
            using var resp = await client.SendAsync(req);

            var key = $"{method} {template}";
            var isExempt = Exemptions.Contains(key);
            var is401 = resp.StatusCode == HttpStatusCode.Unauthorized;

            output.WriteLine($"{(int)resp.StatusCode,-8} {method,-7} {template}{(isExempt ? "   [exempt]" : "")}");

            if (!is401) reachedAnonymously.Add(key);
            if (!isExempt && !is401) leaks.Add($"{key} → {(int)resp.StatusCode}");
            if (isExempt && is401) mislabeledExemptions.Add(key);
        }

        // 1. No protected endpoint may be reachable anonymously.
        leaks.Should().BeEmpty("these endpoints are not on the exemption list but did not return 401");
        // 2. Every exemption must actually be reachable (else it's mis-annotated).
        mislabeledExemptions.Should().BeEmpty("these are exempt but returned 401 — check [AllowAnonymous]");
        // 3. The set reachable anonymously must be EXACTLY the designed exemption list — no surprises.
        reachedAnonymously.Should().BeEquivalentTo(Exemptions,
            "the anonymously-reachable set must equal the designed exemption list exactly");
    }
}
