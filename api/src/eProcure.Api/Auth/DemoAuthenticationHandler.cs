using System.Security.Claims;
using System.Text.Encodings.Web;
using eProcure.Domain.Identity;
using eProcure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace eProcure.Api.Auth;

/// <summary>
/// Turns the demo-only "X-Demo-User" header into a real authenticated <see cref="ClaimsPrincipal"/>
/// so it satisfies the fallback authorization policy (SEC-1/2). The header carries a seeded internal
/// user code (e.g. u_hafiz) or a vendor-login code/email (e.g. VU-sentausa); roles and vendorId are
/// derived server-side, never from a client flag. The scheme is inert unless demo mode is active
/// (<see cref="DemoOptions.IsActive"/>) — the single gate — so it never grants access in Production.
/// </summary>
public sealed class DemoAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Demo";
    private const string HeaderName = "X-Demo-User";

    private readonly DevUserStore _dev;
    private readonly AppDbContext _db;
    private readonly IHostEnvironment _env;
    private readonly DemoOptions _demo;

    public DemoAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DevUserStore dev,
        AppDbContext db,
        IHostEnvironment env,
        IOptions<DemoOptions> demo)
        : base(options, logger, encoder)
    {
        _dev = dev;
        _db = db;
        _env = env;
        _demo = demo.Value;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // The one gate: demo impersonation is inert outside demo mode and hard-blocked in Production.
        if (!DemoOptions.IsActive(_env, _demo))
            return AuthenticateResult.NoResult();

        var header = Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(header))
            return AuthenticateResult.NoResult();

        var claims = new List<Claim>();

        var dev = _dev.Find(header);
        if (dev is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, dev.Id));
            claims.Add(new Claim(ClaimTypes.Name, dev.Name));
            claims.AddRange(dev.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        }
        else
        {
            var vu = await _db.VendorUsers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == header || x.Email == header);
            if (vu is null)
                return AuthenticateResult.Fail($"Unknown demo user '{header}'.");

            claims.Add(new Claim(ClaimTypes.NameIdentifier, vu.Code));
            claims.Add(new Claim(ClaimTypes.Name, vu.Name));
            claims.Add(new Claim(ClaimTypes.Role, Roles.Vendor));
            claims.Add(new Claim("vendorId", vu.VendorId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
