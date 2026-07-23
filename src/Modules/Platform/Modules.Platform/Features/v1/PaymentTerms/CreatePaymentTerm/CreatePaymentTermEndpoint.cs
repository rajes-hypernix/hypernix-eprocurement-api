using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.CreatePaymentTerm;

public static class CreatePaymentTermEndpoint
{
    internal static RouteHandlerBuilder MapCreatePaymentTermEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/payment-terms",
                async (CreatePaymentTermCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreatePaymentTerm")
            .WithSummary("Create a payment term")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}
