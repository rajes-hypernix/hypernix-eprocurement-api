using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.ListPaymentTerms;

public static class ListPaymentTermsEndpoint
{
    internal static RouteHandlerBuilder MapListPaymentTermsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/payment-terms",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListPaymentTermsQuery(activeOnly ?? true), ct)))
            .WithName("ListPaymentTerms")
            .WithSummary("List payment terms")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
