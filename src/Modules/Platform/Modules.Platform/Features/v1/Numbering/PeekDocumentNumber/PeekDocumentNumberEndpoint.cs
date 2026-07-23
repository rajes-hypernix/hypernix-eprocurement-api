using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Numbering.PeekDocumentNumber;

public static class PeekDocumentNumberEndpoint
{
    internal static RouteHandlerBuilder MapPeekDocumentNumberEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/numbering-schemes/{recordType}/peek",
                async (string recordType, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new PeekDocumentNumberQuery(recordType), ct)))
            .WithName("PeekDocumentNumber")
            .WithSummary("Preview the next document number without consuming it")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
