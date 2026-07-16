using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ResolveOnboardingLink;

public static class ResolveOnboardingLinkEndpoint
{
    internal static RouteHandlerBuilder MapResolveOnboardingLinkEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/resolve",
                async (ResolveOnboardingLinkCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("ResolveOnboardingLink")
            .WithSummary("Open an onboarding magic link")
            .AllowAnonymous();
    }
}
