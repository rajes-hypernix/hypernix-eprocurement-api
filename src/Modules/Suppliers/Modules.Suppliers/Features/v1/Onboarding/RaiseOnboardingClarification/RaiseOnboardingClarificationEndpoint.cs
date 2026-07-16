using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RaiseOnboardingClarification;

public static class RaiseOnboardingClarificationEndpoint
{
    internal static RouteHandlerBuilder MapRaiseOnboardingClarificationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/draft/raise-clarification",
                async (RaiseOnboardingClarificationCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("RaiseOnboardingClarification")
            .WithSummary("Vendor raises a clarification back to the buyer")
            .AllowAnonymous();
    }
}
