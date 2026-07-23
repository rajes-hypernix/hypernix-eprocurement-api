using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.ComputePaymentSchedule;

public static class ComputePaymentScheduleEndpoint
{
    internal static RouteHandlerBuilder MapComputePaymentScheduleEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/payment-terms/{id:guid}/schedule",
                async (Guid id, DateOnly baseDate, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ComputePaymentScheduleQuery(id, baseDate), ct)))
            .WithName("ComputePaymentSchedule")
            .WithSummary("Compute the instalment schedule for a payment term")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
