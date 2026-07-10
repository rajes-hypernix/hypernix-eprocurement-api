using System.Security.Claims;
using eProcure.Application.Abstractions;

namespace eProcure.Api.Auth;

/// <summary>
/// Convenience projection of <see cref="ICurrentUser"/> over the request's authenticated
/// <see cref="ClaimsPrincipal"/>. Both real JWTs and the demo scheme (see
/// <see cref="DemoAuthenticationHandler"/>) populate <c>HttpContext.User</c>, so this reads claims
/// only — it makes no authentication decisions and holds no gating logic. Identity is therefore
/// always derived server-side from the principal, never from a client flag (BUSINESS-RULES [G]).
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => Principal?.FindFirstValue(ClaimTypes.Name);
    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
    public Guid? VendorId =>
        Guid.TryParse(Principal?.FindFirstValue("vendorId"), out var vid) ? vid : null;
}
