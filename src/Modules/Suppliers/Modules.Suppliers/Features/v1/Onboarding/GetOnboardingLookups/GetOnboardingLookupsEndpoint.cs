using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingLookups;

public static class GetOnboardingLookupsEndpoint
{
    internal static RouteHandlerBuilder MapGetOnboardingLookupsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // POST, not GET — the token is a bearer-equivalent secret and must not appear in a query string / logs.
        return endpoints.MapPost("/onboarding/lookups",
                async (GetOnboardingLookupsQuery query, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(query, ct)))
            .WithName("GetOnboardingLookups")
            .WithSummary("Get onboarding form lookups (SWEC taxonomy)")
            .AllowAnonymous();
    }
}
