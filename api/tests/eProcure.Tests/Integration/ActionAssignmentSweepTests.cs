using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// The drift-proof guarantee of the role matrix (AUTHORIZATION-MATRIX.md, README-FIRST constraint 6):
/// EVERY registered endpoint outside the exact [AllowAnonymous] exemption list carries exactly one
/// [Action] whose name exists in the ActionCatalog. A new endpoint added without an action assignment
/// — or with a typo'd action the catalog doesn't know — fails here by default. The sweep-test
/// philosophy applied to authorization: the burden of proof is on OPENING, never on closing.
/// </summary>
public sealed class ActionAssignmentSweepTests
{
    // The designed [AllowAnonymous] set — must stay byte-identical to AnonymousSweepTests.Exemptions.
    private static readonly HashSet<string> Exemptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET api/health",
        "GET api/auth/dev-users",
        "POST api/auth/dev-login",
        "POST api/onboarding/resolve",
        "GET api/onboarding/draft",
        "PUT api/onboarding/draft",
        "POST api/onboarding/draft/submit",
        "POST api/onboarding/draft/documents",
        "DELETE api/onboarding/draft/documents/{key}",
        "POST api/onboarding/draft/resubmit",
        "POST api/onboarding/draft/raise-clarification",
    };

    [Fact]
    public async Task Every_authenticated_endpoint_carries_exactly_one_catalog_known_action()
    {
        await using var factory = new TestWebAppFactory();
        var provider = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

        var descriptions = provider.ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Where(d => d.HttpMethod is not null)
            .ToList();

        descriptions.Should().NotBeEmpty("the API must expose endpoints to sweep");

        var unassigned = new List<string>();        // authenticated endpoints missing an [Action]
        var unknownActions = new List<string>();    // [Action] names the catalog doesn't know
        var annotatedExemptions = new List<string>(); // anonymous endpoints wrongly carrying an [Action]
        var usedActions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var d in descriptions)
        {
            var key = $"{d.HttpMethod} {d.RelativePath}";
            var actions = d.ActionDescriptor.EndpointMetadata.OfType<ActionAttribute>().ToList();

            if (Exemptions.Contains(key))
            {
                if (actions.Count > 0)
                    annotatedExemptions.Add(key);
                continue;
            }

            if (actions.Count != 1)
            {
                unassigned.Add($"{key} ({actions.Count} [Action] attributes)");
                continue;
            }

            usedActions.Add(actions[0].Action);
            if (ActionCatalog.RolesFor(actions[0].Action).Count == 0)
                unknownActions.Add($"{key} → '{actions[0].Action}'");
        }

        // 1. Every authenticated endpoint carries exactly one action assignment (constraint 6).
        unassigned.Should().BeEmpty("every endpoint outside the AllowAnonymous set needs exactly one [Action] — assign it a row in docs/AUTHORIZATION-MATRIX.md and the ActionCatalog");
        // 2. Every assigned action exists in the catalog (a typo would deny-all at runtime — surface it here).
        unknownActions.Should().BeEmpty("these [Action] names are not in ActionCatalog.Rules");
        // 3. The token-scoped/demo exemptions carry no action — they are outside the matrix by design.
        annotatedExemptions.Should().BeEmpty("[AllowAnonymous] endpoints must not carry an [Action]");
        // 4. No orphan catalog rows: every action in the catalog is carried by at least one endpoint
        //    STATICALLY, or consumed by a DERIVED dynamic carrier — the metric catalog's per-metric
        //    RequiredAction (A2F-T1: A72 ViewSpendAnalytics exists ONLY there, enforced inside
        //    SystemMetricService.Require; per-role proof lives in RoleMetricScopingTests). The set is
        //    DERIVED from the live catalog, not hand-listed, so a retired metric re-orphans its action
        //    and this sweep goes red again — the no-orphan guarantee is unchanged in strength.
        var dynamicCarriers = eProcure.Infrastructure.Services.SystemMetricService.CatalogRequiredActions;
        ActionCatalog.Rules.Keys.Except(usedActions).Except(dynamicCarriers).Should().BeEmpty(
            "these catalog actions are assigned to no endpoint and no dynamic carrier — remove the row or annotate the endpoint");
    }
}
