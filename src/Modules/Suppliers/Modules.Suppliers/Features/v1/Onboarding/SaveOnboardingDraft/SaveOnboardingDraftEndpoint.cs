using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.SaveOnboardingDraft;

public static class SaveOnboardingDraftEndpoint
{
    internal static RouteHandlerBuilder MapSaveOnboardingDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/onboarding/draft",
                async (SaveOnboardingDraftCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("SaveOnboardingDraft")
            .WithSummary("Save the vendor's onboarding draft (partial patch)")
            .AllowAnonymous();
    }
}
