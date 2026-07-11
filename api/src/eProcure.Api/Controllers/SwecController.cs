using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Suppliers;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/swec")]
public sealed class SwecController(ISwecService swec) : ControllerBase
{
    /// <summary>Flat SWEC taxonomy (build the tree client-side via ParentCode).</summary>
    [HttpGet]
    [Action(ApiActions.ViewSwecTaxonomy)]
    public async Task<ActionResult<IReadOnlyList<SwecCategoryDto>>> List(CancellationToken ct) =>
        Ok(await swec.ListAsync(ct));
}
