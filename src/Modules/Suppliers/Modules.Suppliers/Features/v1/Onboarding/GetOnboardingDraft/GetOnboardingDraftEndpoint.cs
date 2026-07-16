using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingDraft;

public static class GetOnboardingDraftEndpoint
{
    internal static RouteHandlerBuilder MapGetOnboardingDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/onboarding/draft",
                (string token, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetOnboardingDraftQuery(token), ct))
            .WithName("GetOnboardingDraft")
            .WithSummary("Get the vendor's onboarding draft (save-and-resume)")
            .AllowAnonymous();
    }
}
