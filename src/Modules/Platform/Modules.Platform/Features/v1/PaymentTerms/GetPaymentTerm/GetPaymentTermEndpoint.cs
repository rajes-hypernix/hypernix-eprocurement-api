using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.GetPaymentTerm;

public static class GetPaymentTermEndpoint
{
    internal static RouteHandlerBuilder MapGetPaymentTermEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/payment-terms/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetPaymentTermQuery(id), ct)))
            .WithName("GetPaymentTerm")
            .WithSummary("Get a payment term")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
