using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ViewUsers)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken ct) =>
        Ok(await users.ListAsync(ct));

    [HttpGet("vendor-logins")]
    [Action(ApiActions.ViewVendorLogins)]
    public async Task<ActionResult<IReadOnlyList<VendorLoginDto>>> VendorLogins(CancellationToken ct) =>
        Ok(await users.ListVendorLoginsAsync(ct));

    [HttpPost]
    [Action(ApiActions.ManageUsers)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest req, CancellationToken ct) =>
        Ok(await users.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Action(ApiActions.ManageUsers)]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct) =>
        Ok(await users.UpdateAsync(id, req, ct));
}
