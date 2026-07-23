using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Banks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Banks.UpdateBank;

public static class UpdateBankEndpoint
{
    internal static RouteHandlerBuilder MapUpdateBankEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/banks/{id:guid}",
                async (Guid id, UpdateBankBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(
                        new UpdateBankCommand(id, body.Name, body.CountryCode, body.SwiftCode),
                        ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateBank")
            .WithSummary("Update a bank")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }

    private sealed record UpdateBankBody(string Name, string CountryCode, string? SwiftCode = null);
}
