using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.DeleteOnboardingDocument;

public static class DeleteOnboardingDocumentEndpoint
{
    internal static RouteHandlerBuilder MapDeleteOnboardingDocumentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/onboarding/draft/documents/{key}",
                async (string key, string token, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new DeleteOnboardingDocumentCommand(token, key), ct).ConfigureAwait(false);
                    return Results.NoContent();
                })
            .WithName("DeleteOnboardingDocument")
            .WithSummary("Remove an onboarding document")
            .AllowAnonymous();
    }
}
