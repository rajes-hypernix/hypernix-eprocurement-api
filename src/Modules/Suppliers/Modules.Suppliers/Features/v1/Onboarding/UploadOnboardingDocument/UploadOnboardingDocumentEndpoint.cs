using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.UploadOnboardingDocument;

public static class UploadOnboardingDocumentEndpoint
{
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    internal static RouteHandlerBuilder MapUploadOnboardingDocumentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/draft/documents",
                async (string token, string key, IFormFile file, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(file);
                    await using var stream = file.OpenReadStream();
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer, ct).ConfigureAwait(false);

                    var command = new UploadOnboardingDocumentCommand(token, key, file.FileName, file.ContentType, buffer.ToArray());
                    return Results.Ok(await mediator.Send(command, ct));
                })
            .WithName("UploadOnboardingDocument")
            .WithSummary("Upload an onboarding document")
            .Accepts<IFormFile>("multipart/form-data")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .AllowAnonymous();
    }
}
