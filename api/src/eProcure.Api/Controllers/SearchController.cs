using eProcure.Application.Search;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>
/// Global search (D2 navigation shell). Authenticated by the fallback policy —
/// deliberately NOT on the anonymous exemption list (the Slice F sweep test
/// must pass unchanged). Read-only; scoping lives in SearchService's queries.
/// </summary>
[ApiController]
[Route("api/search")]
public sealed class SearchController(ISearchService search) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchHit>>> Search([FromQuery] string? q, CancellationToken ct) =>
        Ok(await search.SearchAsync(q ?? "", ct));
}
