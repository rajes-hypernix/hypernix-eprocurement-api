using eProcure.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace eProcure.Api.Auth;

/// <summary>
/// Resolves "action:*" policies from the <see cref="ActionCatalog"/> — the single seam between
/// the declarative matrix and ASP.NET authorization. An action absent from the catalog yields a
/// deny-all policy (fail-closed): a typo can close an endpoint but never open one. Everything
/// else (the fallback deny-anonymous policy, the named role policies) delegates to the default
/// provider, so Slice F's 401 perimeter is byte-identical.
/// </summary>
public sealed class ActionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(ActionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return _fallback.GetPolicyAsync(policyName);

        var action = policyName[ActionAttribute.PolicyPrefix.Length..];
        var roles = ActionCatalog.RolesFor(action);

        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser();
        if (roles.Count > 0)
            policy.RequireRole(roles);
        else
            policy.RequireAssertion(_ => false); // unknown action → deny all (fail-closed)

        return Task.FromResult<AuthorizationPolicy?>(policy.Build());
    }
}
