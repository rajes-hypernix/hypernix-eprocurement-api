using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// HTTP-level regression for the demo gate (SEC-1): X-Demo-User must be rejected in Production even
/// with Demo:Enabled=true — the hard block that config cannot override — and must work in a
/// demo-active environment (the control). Complements the handler-level DemoIdentityTests.
/// </summary>
public sealed class DemoGatingTests
{
    [Fact]
    public async Task X_Demo_User_is_rejected_in_Production_even_when_Enabled()
    {
        await using var factory = new TestWebAppFactory(environment: "Production", demoEnabled: true);
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Demo-User", "u_faridah");

        var resp = await client.GetAsync("/api/rfqs");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the demo scheme is inert in Production, so the header grants nothing and the fallback policy 401s");
    }

    [Fact]
    public async Task X_Demo_User_is_honoured_when_demo_mode_is_active()
    {
        await using var factory = new TestWebAppFactory(environment: "Testing", demoEnabled: true);
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Demo-User", "u_faridah");

        var resp = await client.GetAsync("/api/rfqs");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "demo mode is active — the persona authenticates");
    }

    // TEST-SWEEP-T1: the inventory demands the 404 EXPLICITLY (not just the 401 fallback) —
    // dev-users/dev-login are [AllowAnonymous] so in Production they must vanish (404), and in
    // a demo-active environment they must work.
    [Fact]
    public async Task Dev_login_endpoints_are_404_in_Production_and_work_when_demo_is_active()
    {
        await using var prod = new TestWebAppFactory(environment: "Production", demoEnabled: true);
        var prodClient = prod.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await prodClient.GetAsync("/api/auth/dev-users")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "demo-only endpoints are inert in Production regardless of config");
        (await prodClient.PostAsJsonAsync("/api/auth/dev-login", new { user = "u_faridah" })).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        await using var demo = new TestWebAppFactory(environment: "Testing", demoEnabled: true);
        var demoClient = demo.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await demoClient.GetAsync("/api/auth/dev-users")).StatusCode.Should().Be(HttpStatusCode.OK,
            "the same endpoints bootstrap login when demo mode is active");
    }
}
