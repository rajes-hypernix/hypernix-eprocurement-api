using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.UpdatePaymentTerm;

public static class UpdatePaymentTermEndpoint
{
    internal static RouteHandlerBuilder MapUpdatePaymentTermEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/payment-terms/{id:guid}",
                async (Guid id, UpdatePaymentTermBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(
                        new UpdatePaymentTermCommand(
                            id,
                            body.Name,
                            body.Kind,
                            body.DueDays,
                            body.DayOfMonth,
                            body.MonthsAhead,
                            body.MinimumDaysBeforeDue,
                            body.DiscountPct,
                            body.DiscountDays,
                            body.Rows),
                        ct);
                    return Results.Ok(result);
                })
            .WithName("UpdatePaymentTerm")
            .WithSummary("Update a payment term")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record UpdatePaymentTermBody(
        string Name,
        string Kind,
        int? DueDays = null,
        int? DayOfMonth = null,
        int? MonthsAhead = null,
        int? MinimumDaysBeforeDue = null,
        decimal? DiscountPct = null,
        int? DiscountDays = null,
        IReadOnlyList<PaymentScheduleRowInput>? Rows = null);
}
