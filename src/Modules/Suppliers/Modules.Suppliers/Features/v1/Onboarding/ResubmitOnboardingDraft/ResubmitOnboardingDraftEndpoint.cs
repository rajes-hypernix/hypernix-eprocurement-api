using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ResubmitOnboardingDraft;

public static class ResubmitOnboardingDraftEndpoint
{
    internal static RouteHandlerBuilder MapResubmitOnboardingDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/draft/resubmit",
                async (ResubmitOnboardingDraftCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("ResubmitOnboardingDraft")
            .WithSummary("Answer a clarification round and resubmit")
            .AllowAnonymous();
    }
}
