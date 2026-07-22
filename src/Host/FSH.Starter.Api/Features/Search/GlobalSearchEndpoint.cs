using Asp.Versioning;
using FSH.Modules.Platform.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Starter.Api.Features.Search;

internal static class GlobalSearchEndpoint
{
    public static void MapGlobalSearch(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapGet("/api/v{version:apiVersion}/search",
                async (string? q, GlobalSearchService search, CancellationToken ct) =>
                {
                    IReadOnlyList<SearchHitDto> hits = await search.SearchAsync(q, ct).ConfigureAwait(false);
                    return Results.Ok(hits);
                })
            .WithApiVersionSet(versionSet)
            .WithName("GlobalSearch")
            .WithSummary("Cross-module global search")
            .WithTags("Search")
            .RequireAuthorization()
            .Produces<IReadOnlyList<SearchHitDto>>();
    }
}
