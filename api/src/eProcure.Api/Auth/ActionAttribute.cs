using Microsoft.AspNetCore.Authorization;

namespace eProcure.Api.Auth;

/// <summary>
/// Assigns an endpoint its AUTHORIZATION-MATRIX action: <c>[Action(ApiActions.SubmitBid)]</c>.
/// Resolves to the "action:{name}" policy, which <see cref="ActionPolicyProvider"/> builds from
/// <see cref="eProcure.Application.Authorization.ActionCatalog"/>. Every authenticated endpoint
/// must carry exactly one (drift-proof: ActionAssignmentSweepTests).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ActionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "action:";

    public ActionAttribute(string action) : base(PolicyPrefix + action) => Action = action;

    public string Action { get; }
}
