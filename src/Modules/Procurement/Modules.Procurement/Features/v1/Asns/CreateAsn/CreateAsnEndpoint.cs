using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Asns;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Asns.CreateAsn;

public static class CreateAsnEndpoint
{
    internal static RouteHandlerBuilder MapCreateAsnEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/asns",
                async (CreateAsnCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(command, ct);
                    return Results.Ok(result);
                })
            .WithName("CreateAsn")
            .WithSummary("Create an advance shipping notice for a purchase order")
            .RequirePermission(ProcurementPermissions.Deliveries.CreateAsn);
    }
}
