using System.Net;
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
}
