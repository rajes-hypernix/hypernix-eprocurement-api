using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Numbering.UpdateNumberingScheme;

public static class UpdateNumberingSchemeEndpoint
{
    internal static RouteHandlerBuilder MapUpdateNumberingSchemeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/numbering-schemes/{recordType}",
                async (string recordType, UpdateNumberingSchemeBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(
                        new UpdateNumberingSchemeCommand(recordType, body.Prefix, body.YearSegment, body.Digits),
                        ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateNumberingScheme")
            .WithSummary("Update a document numbering scheme")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record UpdateNumberingSchemeBody(string Prefix, bool YearSegment, int Digits);
}
