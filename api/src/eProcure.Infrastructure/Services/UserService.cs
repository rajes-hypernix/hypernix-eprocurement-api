using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Identity;
using eProcure.Domain.Identity;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class UserService(
    AppDbContext db,
    IClock clock,
    ICodeGenerator codes,
    IAuditLog audit) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct);
        return users.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<VendorLoginDto>> ListVendorLoginsAsync(CancellationToken ct = default)
    {
        var rows = await (from vu in db.VendorUsers.AsNoTracking()
                          join v in db.Vendors.AsNoTracking() on vu.VendorId equals v.Id
                          orderby v.Name
                          select new VendorLoginDto(vu.Id, vu.Code, v.Name, vu.Name, vu.Email, vu.IsActive, v.Id))
                         .ToListAsync(ct);
        return rows;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        // User constructor enforces the SoD guard (no Vendor role on internal users).
        var user = new User(await codes.NextAsync("USR", ct), req.Name, req.Email, req.Roles)
        {
            CreatedUtc = clock.UtcNow,
            UpdatedUtc = clock.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("User", user.Code, "Created", after: user.Name, ct: ct);
        return Map(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException($"User {id} not found.");
        var beforeRoles = string.Join(", ", user.Roles);
        user.Name = req.Name;
        user.Email = req.Email;
        user.SetRoles(req.Roles);       // SoD guard
        user.IsActive = req.IsActive;
        user.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("User", user.Code, "Updated",
            before: beforeRoles, after: string.Join(", ", user.Roles), ct: ct);
        return Map(user);
    }

    private static UserDto Map(User u) =>
        new(u.Id, u.Code, u.Name, u.Email, u.Roles, u.IsActive);
}
