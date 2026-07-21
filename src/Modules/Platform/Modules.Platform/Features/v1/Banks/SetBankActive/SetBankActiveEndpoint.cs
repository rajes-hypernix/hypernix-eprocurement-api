using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Banks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Banks.SetBankActive;

public static class SetBankActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetBankActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/banks/{id:guid}/active",
                async (Guid id, SetBankActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetBankActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetBankActive")
            .WithSummary("Activate or deactivate a bank")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }

    private sealed record SetBankActiveBody(bool IsActive);
}
