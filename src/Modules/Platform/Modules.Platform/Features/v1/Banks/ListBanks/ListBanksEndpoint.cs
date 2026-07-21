using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Banks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Banks.ListBanks;

public static class ListBanksEndpoint
{
    internal static RouteHandlerBuilder MapListBanksEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/banks",
                async (string? countryCode, bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListBanksQuery(countryCode, activeOnly ?? true), ct);
                    return Results.Ok(result);
                })
            .WithName("ListBanks")
            .WithSummary("List banks")
            .RequirePermission(PlatformPermissions.Lookups.View);
    }
}
