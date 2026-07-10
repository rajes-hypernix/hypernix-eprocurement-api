using eProcure.Application;
using eProcure.Domain;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Middleware;

/// <summary>
/// Maps domain/application exceptions to ProblemDetails:
/// NotFound→404, Forbidden→403, DomainRule→409 (CONVENTIONS.md).
/// </summary>
public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (NotFoundException ex)
        {
            await Write(ctx, StatusCodes.Status404NotFound, "Not found", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await Write(ctx, StatusCodes.Status403Forbidden, "Forbidden", ex.Message);
        }
        catch (DomainRuleException ex)
        {
            await Write(ctx, StatusCodes.Status409Conflict, "Rule violation", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await Write(ctx, StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.");
        }
    }

    private static async Task Write(HttpContext ctx, int status, string title, string detail)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
        });
    }
}
