namespace eProcure.Application.Identity;

public sealed record UserDto(
    Guid Id,
    string Code,
    string Name,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed record VendorLoginDto(
    Guid Id,
    string Code,
    string VendorName,
    string Name,
    string Email,
    bool IsActive,
    Guid VendorId);

public sealed record CreateUserRequest(string Name, string Email, IReadOnlyList<string> Roles);
public sealed record UpdateUserRequest(string Name, string Email, IReadOnlyList<string> Roles, bool IsActive);

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VendorLoginDto>> ListVendorLoginsAsync(CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default);
}
