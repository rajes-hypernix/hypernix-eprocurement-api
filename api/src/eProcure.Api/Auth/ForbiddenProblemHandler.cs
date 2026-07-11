using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Auth;

/// <summary>
/// Authenticated-but-wrong-role → 403 with the app's existing ProblemDetails shape
/// ("Not permitted for your role"), matching ExceptionMiddleware's ForbiddenException rendering.
/// The 401 challenge path (no principal → fallback policy) is delegated untouched, so the
/// anonymous-sweep behaviour is unchanged; the demo endpoints' 404 existence-hiding lives inside
/// [AllowAnonymous] actions and never reaches this handler.
/// </summary>
public sealed class ForbiddenProblemHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "Not permitted for your role.",
            });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
