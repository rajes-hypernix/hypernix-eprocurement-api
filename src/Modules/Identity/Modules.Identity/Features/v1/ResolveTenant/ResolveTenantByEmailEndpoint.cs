using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.v1.ResolveTenant;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.ComponentModel;

namespace FSH.Modules.Identity.Features.v1.ResolveTenant;

public static class ResolveTenantByEmailEndpoint
{
    internal static RouteHandlerBuilder MapResolveTenantByEmailEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/resolve-tenant",
                async Task<Results<Ok<ResolveTenantByEmailResponse>, NotFound<ProblemDetails>, Conflict<ResolveTenantByEmailResponse>>> (
                    [FromBody] ResolveTenantByEmailQuery body,
                    [DefaultValue("root")][FromHeader(Name = MultitenancyConstants.Identifier)] string tenant,
                    IMediator mediator,
                    CancellationToken cancellationToken) =>
                {
                    // Tenant header only primes IdentityDbContext (shared DB); lookup ignores filters.
                    _ = tenant;
                    var result = await mediator.Send(body, cancellationToken).ConfigureAwait(false);

                    return result.Status switch
                    {
                        ResolveTenantStatus.Found => TypedResults.Ok(result),
                        ResolveTenantStatus.Ambiguous => TypedResults.Conflict(result),
                        _ => TypedResults.NotFound(new ProblemDetails
                        {
                            Status = StatusCodes.Status404NotFound,
                            Title = "Tenant not found",
                            Detail = "No active organization could be resolved for that email.",
                        }),
                    };
                })
            .WithName("ResolveTenantByEmail")
            .WithSummary("Resolve tenant from email")
            .WithDescription("Looks up which tenant owns the given email so clients can omit the tenant field at login. Returns 404 when unknown, 409 when multiple tenants match.")
            .AllowAnonymous()
            .Produces<ResolveTenantByEmailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ResolveTenantByEmailResponse>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
