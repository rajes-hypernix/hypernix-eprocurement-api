using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Domain.Identity;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// The authorization test matrix (AUTHORIZATION-MATRIX.md, Phase 2) — GENERATED from the
/// ActionCatalog so the matrix and its tests cannot diverge. For every action, for every role
/// (one PURE principal per role — no seeded dev user holds Approver alone — plus a vendor
/// principal): a denied role gets exactly the role-gate 403 ("Not permitted for your role");
/// an allowed role gets past the role gate — any non-401 status that is NOT the role-gate 403
/// is acceptable (200/204/400/404/409, or a RESOURCE-level 403/500 from the service layer,
/// e.g. FileAccessPolicy's deny-on-uncertainty for a nonexistent file). We assert
/// authorization, not workflow.
/// </summary>
public sealed class RoleMatrixFixture : IAsyncLifetime
{
    public const string RoleGateDetail = "Not permitted for your role.";

    /// <summary>Pure per-role personas (Testing-only, appended via TestWebAppFactory).</summary>
    public static readonly IReadOnlyDictionary<string, string> PersonaByRole = new Dictionary<string, string>
    {
        [Roles.Buyer] = "rm_buyer",
        [Roles.Approver] = "rm_approver",
        [Roles.TechEvaluator] = "rm_tech",
        [Roles.CommEvaluator] = "rm_comm",
        [Roles.Admin] = "rm_admin",
        [Roles.Vendor] = "VU-RM",
    };

    public TestWebAppFactory Factory { get; } = new(extraDevUsers:
    [
        new DevUser("rm_buyer", "RM Buyer", "rm_buyer@test", [Roles.Buyer]),
        new DevUser("rm_approver", "RM Approver", "rm_approver@test", [Roles.Approver]),
        new DevUser("rm_tech", "RM Tech", "rm_tech@test", [Roles.TechEvaluator]),
        new DevUser("rm_comm", "RM Comm", "rm_comm@test", [Roles.CommEvaluator]),
        new DevUser("rm_admin", "RM Admin", "rm_admin@test", [Roles.Admin]),
    ]);

    public async Task InitializeAsync()
    {
        await Factory.SeedAsync(db =>
        {
            var v = new Vendor { Code = "V-RM", Name = "RoleMatrix", RegisteredName = "RoleMatrix Sdn Bhd" };
            db.Vendors.Add(v);
            db.VendorUsers.Add(new VendorUser("VU-RM", v.Id, "RM Vendor User", "rm@vendor.test"));
            return Task.CompletedTask;
        });
    }

    public Task DisposeAsync() { Factory.Dispose(); return Task.CompletedTask; }

    public HttpClient ClientAs(string demoUser)
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Demo-User", demoUser);
        return client;
    }

    /// <summary>All (method, template) endpoints carrying the given action.</summary>
    public IReadOnlyList<(string Method, string Template)> EndpointsFor(string action) =>
        Factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Where(d => d.HttpMethod is not null &&
                        d.ActionDescriptor.EndpointMetadata.OfType<ActionAttribute>().Any(a => a.Action == action))
            .Select(d => (d.HttpMethod!, d.RelativePath ?? ""))
            .Distinct()
            .ToList();
}

public sealed class RoleMatrixTests(RoleMatrixFixture fx) : IClassFixture<RoleMatrixFixture>
{
    public static TheoryData<string> Actions()
    {
        var data = new TheoryData<string>();
        foreach (var action in ActionCatalog.Rules.Keys.OrderBy(a => a, StringComparer.Ordinal))
            data.Add(action);
        return data;
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Allowed_roles_pass_the_gate_and_denied_roles_get_exactly_403(string action)
    {
        var endpoints = fx.EndpointsFor(action);
        if (endpoints.Count == 0)
        {
            // A2F-T1: an action may live ONLY as a dynamic carrier (a metric's RequiredAction —
            // A72 ViewSpendAnalytics). Its per-role 403/200 proof is RoleMetricScopingTests;
            // here we pin that it is a KNOWN dynamic carrier, so a truly orphaned row still fails.
            eProcure.Infrastructure.Services.SystemMetricService.CatalogRequiredActions
                .Should().Contain(action, $"action {action} is carried by no endpoint — it must be a dynamic carrier or be removed (drift sweep)");
            return;
        }

        var allowedRoles = ActionCatalog.RolesFor(action);
        var failures = new List<string>();

        foreach (var role in Roles.All)
        {
            var persona = RoleMatrixFixture.PersonaByRole[role];
            var allowed = allowedRoles.Contains(role);
            using var client = fx.ClientAs(persona);

            foreach (var (method, template) in endpoints)
            {
                var path = "/" + Regex.Replace(template, "\\{[^}]+\\}", "00000000-0000-0000-0000-000000000001");
                using var req = new HttpRequestMessage(new HttpMethod(method), path);
                if (template == "api/files" && method == "POST")
                {
                    // IFormFile infers [Consumes(multipart/form-data)], an ACTION CONSTRAINT: a JSON
                    // request 415s at routing before authorization ever runs. Send real multipart so
                    // the endpoint matches and the role gate is what we measure.
                    var mp = new MultipartFormDataContent { { new ByteArrayContent([1]), "file", "probe.bin" } };
                    req.Content = mp;
                }
                else if (method is "POST" or "PUT")
                    req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                using var resp = await client.SendAsync(req);

                var isRoleGate403 = resp.StatusCode == HttpStatusCode.Forbidden && await IsRoleGateBody(resp);

                if (allowed)
                {
                    // The role gate must NOT fire; 401 must never appear for an authenticated persona.
                    if (resp.StatusCode == HttpStatusCode.Unauthorized)
                        failures.Add($"{role}/{persona} {method} {template} → 401 (allowed role must authenticate)");
                    else if (isRoleGate403)
                        failures.Add($"{role}/{persona} {method} {template} → role-gate 403 (catalog allows this role)");
                }
                else if (resp.StatusCode != HttpStatusCode.Forbidden)
                {
                    failures.Add($"{role}/{persona} {method} {template} → {(int)resp.StatusCode} (denied role must get 403)");
                }
            }
        }

        failures.Should().BeEmpty($"the {action} row of the catalog must hold for every role");
    }

    private static async Task<bool> IsRoleGateBody(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("detail", out var d) &&
                   d.GetString() == RoleMatrixFixture.RoleGateDetail;
        }
        catch (JsonException) { return false; }
    }
}
