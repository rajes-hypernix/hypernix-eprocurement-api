using System.Security.Claims;
using System.Text.Encodings.Web;
using eProcure.Api.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// SEC-1/X10: demo impersonation (X-Demo-User) is gated by environment AND config. After the Slice F
/// rewiring the gate lives in <see cref="DemoAuthenticationHandler"/> (which issues the ClaimsPrincipal
/// the fallback policy needs). These re-home the Hardening-1 regression coverage against the handler —
/// the Production hard-block in particular must never regress.
/// </summary>
public sealed class DemoIdentityTests
{
    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "eProcure.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    /// <summary>Runs the demo handler over an X-Demo-User request in the given env/config and returns
    /// the authentication result.</summary>
    private static async Task<AuthenticateResult> AuthenticateAsync(
        string environmentName, bool demoEnabled, string header = "u_faridah")
    {
        var handler = new DemoAuthenticationHandler(
            new StubOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            new DevUserStore(),
            TestDb.NewContext(),
            new FakeHostEnvironment(environmentName),
            Options.Create(new DemoOptions { Enabled = demoEnabled }));

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Demo-User"] = header;
        await handler.InitializeAsync(
            new AuthenticationScheme(DemoAuthenticationHandler.SchemeName, null, typeof(DemoAuthenticationHandler)),
            ctx);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task Handler_is_inert_in_Production_even_when_Enabled()
    {
        var result = await AuthenticateAsync("Production", demoEnabled: true);
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();   // no principal → request stays anonymous → fallback policy 401s
    }

    [Fact]
    public async Task Handler_authenticates_the_persona_in_Development()
    {
        var result = await AuthenticateAsync("Development", demoEnabled: false);   // config irrelevant in Dev
        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("u_faridah");
        result.Principal!.FindAll(ClaimTypes.Role).Select(c => c.Value).Should().Contain("Buyer");
    }

    [Fact]
    public async Task Handler_authenticates_in_Staging_when_Enabled()
    {
        var result = await AuthenticateAsync("Staging", demoEnabled: true);
        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("u_faridah");
    }

    [Fact]
    public async Task Handler_is_inert_in_Staging_when_disabled()
    {
        var result = await AuthenticateAsync("Staging", demoEnabled: false);
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
    }
}
