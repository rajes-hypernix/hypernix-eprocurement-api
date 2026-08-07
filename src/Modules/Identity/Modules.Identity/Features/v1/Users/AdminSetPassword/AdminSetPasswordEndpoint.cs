using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Identity.Contracts.Authorization;
using FSH.Modules.Identity.Contracts.v1.Users.AdminSetPassword;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Identity.Features.v1.Users.AdminSetPassword;

public static class AdminSetPasswordEndpoint
{
    internal static RouteHandlerBuilder MapAdminSetPasswordEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/users/{id:guid}/set-password", Handler)
            .WithName("AdminSetPassword")
            .WithSummary("Set a user's password (admin)")
            .RequirePermission(IdentityPermissions.Users.Update)
            .WithDescription("Admin sets a new password for a user without knowing the current password.")
            .Produces<string>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<Results<Ok<string>, BadRequest>> Handler(
        string id,
        [FromBody] AdminSetPasswordCommand command,
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

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }
}
