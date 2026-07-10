using Microsoft.Extensions.Hosting;

namespace eProcure.Api.Auth;

/// <summary>
/// Gate for the demo-only affordances: X-Demo-User impersonation (<see cref="CurrentUser"/>) and the
/// dev-login / dev-users endpoints. Bound from the "Demo" config section (default disabled).
/// The impersonation fallback lets any caller become any principal, so it is gated by environment
/// AND explicit config (SEC-1/X10):
/// <list type="bullet">
///   <item>Development — always active; local demos are unchanged.</item>
///   <item>Production — always inert; a hard block that no config value can override.</item>
///   <item>Any other environment (e.g. Staging) — active only when <see cref="Enabled"/> is true.</item>
/// </list>
/// </summary>
public sealed class DemoOptions
{
    public bool Enabled { get; set; }

    /// <summary>The single source of truth for "is demo mode live right now?".</summary>
    public static bool IsActive(IHostEnvironment env, DemoOptions options)
    {
        if (env.IsProduction()) return false;   // hard block, config cannot re-enable
        if (env.IsDevelopment()) return true;   // local demos unchanged
        return options.Enabled;                 // Staging/custom: explicit opt-in only
    }
}
