using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.SetPaymentTermActive;

public static class SetPaymentTermActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetPaymentTermActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/payment-terms/{id:guid}/active",
                async (Guid id, SetPaymentTermActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetPaymentTermActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetPaymentTermActive")
            .WithSummary("Activate or deactivate a payment term")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record SetPaymentTermActiveBody(bool IsActive);
}
