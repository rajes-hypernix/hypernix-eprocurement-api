using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.SubmitOnboardingDraft;

public static class SubmitOnboardingDraftEndpoint
{
    internal static RouteHandlerBuilder MapSubmitOnboardingDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/draft/submit",
                async (SubmitOnboardingDraftCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("SubmitOnboardingDraft")
            .WithSummary("Submit the completed onboarding form")
            .AllowAnonymous();
    }
}
