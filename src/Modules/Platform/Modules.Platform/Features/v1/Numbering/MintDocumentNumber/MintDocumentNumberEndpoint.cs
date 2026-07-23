using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Numbering.MintDocumentNumber;

public static class MintDocumentNumberEndpoint
{
    internal static RouteHandlerBuilder MapMintDocumentNumberEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/numbering-schemes/{recordType}/mint",
                async (string recordType, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new MintDocumentNumberCommand(recordType), ct)))
            .WithName("MintDocumentNumber")
            .WithSummary("Mint and consume the next document number")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}
