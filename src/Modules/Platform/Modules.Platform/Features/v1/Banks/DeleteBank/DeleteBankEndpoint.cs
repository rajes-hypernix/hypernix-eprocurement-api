using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Banks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Banks.DeleteBank;

public static class DeleteBankEndpoint
{
    internal static RouteHandlerBuilder MapDeleteBankEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/banks/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new DeleteBankCommand(id), ct);
                    return Results.NoContent();
                })
            .WithName("DeleteBank")
            .WithSummary("Soft-delete a bank")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }
}
