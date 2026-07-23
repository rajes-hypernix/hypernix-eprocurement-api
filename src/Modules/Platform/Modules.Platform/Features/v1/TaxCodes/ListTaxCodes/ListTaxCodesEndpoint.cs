using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.TaxCodes.ListTaxCodes;

public static class ListTaxCodesEndpoint
{
    internal static RouteHandlerBuilder MapListTaxCodesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/tax-codes",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListTaxCodesQuery(activeOnly ?? true), ct)))
            .WithName("ListTaxCodes")
            .WithSummary("List tax codes")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
