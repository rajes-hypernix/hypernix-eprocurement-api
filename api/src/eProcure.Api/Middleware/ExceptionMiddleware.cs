using eProcure.Application;
using eProcure.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Api.Middleware;

/// <summary>
/// Maps domain/application exceptions to ProblemDetails:
/// NotFound→404, Forbidden→403, DomainRule→409, concurrency conflict→409 (CONVENTIONS.md).
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
        catch (Application.Views.ViewValidationException ex)
        {
            // D3 ruling: an invalid view definition is a 400 — loud on save AND run,
            // never a silently dropped filter.
            await Write(ctx, StatusCodes.Status400BadRequest, "Invalid view definition", ex.Message);
        }
        catch (Application.Dashboards.DashboardValidationException ex)
        {
            // D4: same loud-validation posture for portlet configs and dashboard edits.
            await Write(ctx, StatusCodes.Status400BadRequest, "Invalid dashboard definition", ex.Message);
        }
        catch (Application.CustomFields.CustomFieldValidationException ex)
        {
            // D5: same loud posture for custom field defs and values.
            await Write(ctx, StatusCodes.Status400BadRequest, "Invalid custom field data", ex.Message);
        }
        catch (DomainRuleException ex)
        {
            await Write(ctx, StatusCodes.Status409Conflict, "Rule violation", ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Optimistic-concurrency clash (xmin token, T2): someone else modified this record since
            // it was loaded. Surface a distinct 409 telling the user to reload — never swallow or
            // auto-retry (a blind retry would clobber the other write). Closes RFQ-LIFECYCLE E11.
            await Write(ctx, StatusCodes.Status409Conflict, "Concurrent modification",
                "This record was changed by someone else since you loaded it. Reload and try again.");
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
