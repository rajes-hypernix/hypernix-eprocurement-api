using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Banks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Banks.CreateBank;

public static class CreateBankEndpoint
{
    internal static RouteHandlerBuilder MapCreateBankEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/banks",
                async (CreateBankCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateBank")
            .WithSummary("Create a bank")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }
}
