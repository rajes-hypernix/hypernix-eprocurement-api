using eProcure.Api.Auth;
using eProcure.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    DevUserStore users, JwtTokenService tokens, IUserService userService,
    IHostEnvironment env, IOptions<DemoOptions> demo) : ControllerBase
{
    // dev-login / dev-users are demo-only (they mint fully-roled JWTs with no password). They 404
    // when demo mode is inactive — inert in Production regardless of config (SEC-1).
    private bool DemoActive => DemoOptions.IsActive(env, demo.Value);

    public sealed record DevUserDto(string Id, string Name, string Email, string[] Roles);
    public sealed record DevLoginRequest(string User);
    public sealed record LoginResponse(string Token, DateTime ExpiresUtc, DevUserDto User);
    public sealed record PersonaDto(string Code, string Name, string Kind, string[] Roles, string? VendorName, Guid? VendorId);

    /// <summary>Lists the seeded internal users available for dev login (SEED-DATA §2). Anonymous so
    /// it can bootstrap a login, but internally demo-gated (404 when demo mode is inactive).</summary>
    [AllowAnonymous]
    [HttpGet("dev-users")]
    public ActionResult<IEnumerable<DevUserDto>> DevUsers()
    {
        if (!DemoActive) return NotFound();
        return Ok(users.Users.Select(u => new DevUserDto(u.Id, u.Name, u.Email, u.Roles)));
    }

    /// <summary>
    /// Switchable demo personas (internal users + vendor logins) for the "Act as" picker.
    /// The chosen code is sent as the X-Demo-User header; the server derives roles/vendor
    /// from it, so masking and access scoping cannot be defeated client-side.
    /// </summary>
    [HttpGet("personas")]
    public async Task<ActionResult<IEnumerable<PersonaDto>>> Personas(CancellationToken ct)
    {
        var internals = users.Users.Select(u => new PersonaDto(u.Id, u.Name, "internal", u.Roles, null, null));
        var vendorLogins = (await userService.ListVendorLoginsAsync(ct))
            .Select(v => new PersonaDto(v.Code, v.Name, "vendor", ["Vendor"], v.VendorName, v.VendorId));
        return Ok(internals.Concat(vendorLogins));
    }

    /// <summary>
    /// Dev-only login: issues a JWT (with role claims) for a seeded user by id or
    /// email. Anonymous so it can bootstrap a login, but internally demo-gated (404 when demo mode is
    /// inactive). Real auth replaces this later; it exists so the demo is walkable.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("dev-login")]
    public ActionResult<LoginResponse> DevLogin([FromBody] DevLoginRequest request)
    {
        if (!DemoActive) return NotFound();
        var user = users.Find(request.User);
        if (user is null)
            return NotFound(new { error = $"Unknown dev user '{request.User}'." });

        var (token, expires) = tokens.Issue(user);
        return Ok(new LoginResponse(
            token,
            expires,
            new DevUserDto(user.Id, user.Name, user.Email, user.Roles)));
    }
}
