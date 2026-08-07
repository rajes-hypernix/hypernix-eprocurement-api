using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Identity.Contracts.Authorization;
using FSH.Modules.Identity.Contracts.v1.Users.AdminUpdateUser;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Identity.Features.v1.Users.AdminUpdateUser;

public static class AdminUpdateUserEndpoint
{
    internal static RouteHandlerBuilder MapAdminUpdateUserEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/users/{id:guid}", Handler)
            .WithName("AdminUpdateUser")
            .WithSummary("Update a user (admin)")
            .RequirePermission(IdentityPermissions.Users.Update)
            .WithDescription("Update first name, last name, phone, and email for any user in the tenant.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<Results<NoContent, BadRequest>> Handler(
        string id,
        [FromBody] AdminUpdateUserCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            command.UserId = id;
        }

        if (!string.Equals(id, command.UserId, StringComparison.Ordinal))
        {
            return TypedResults.BadRequest();
        }

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
