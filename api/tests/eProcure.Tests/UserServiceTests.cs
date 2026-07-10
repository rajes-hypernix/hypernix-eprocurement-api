using eProcure.Application.Identity;
using eProcure.Domain;
using eProcure.Domain.Identity;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class UserServiceTests
{
    private static UserService NewService(out TestContext c)
    {
        c = TestContext.New();
        return new UserService(c.Db, c.Clock, c.Codes, c.Audit);
    }

    [Fact]
    public async Task CreateAsync_InternalRoles_PersistsAndAudits()
    {
        var svc = NewService(out var c);
        var dto = await svc.CreateAsync(new CreateUserRequest("Lim Chee Kong", "lim@hypernix.test",
            [Roles.Buyer, Roles.Approver]));

        dto.Roles.Should().BeEquivalentTo([Roles.Buyer, Roles.Approver]);
        (await c.Db.Users.CountAsync()).Should().Be(1);
        (await c.Db.AuditEntries.AnyAsync(a => a.EntityType == "User" && a.Action == "Created"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithVendorRole_IsRejected_SoD()
    {
        var svc = NewService(out _);
        var act = () => svc.CreateAsync(new CreateUserRequest("Bad", "bad@hypernix.test",
            [Roles.Buyer, Roles.Vendor]));

        await act.Should().ThrowAsync<DomainRuleException>()
            .WithMessage("*Vendor*segregation of duties*");
    }

    [Fact]
    public async Task UpdateAsync_AddingVendorRole_IsRejected_SoD()
    {
        var svc = NewService(out var c);
        var created = await svc.CreateAsync(new CreateUserRequest("Faridah", "faridah@hypernix.test", [Roles.Buyer]));

        var act = () => svc.UpdateAsync(created.Id,
            new UpdateUserRequest("Faridah", "faridah@hypernix.test", [Roles.Vendor], true));

        await act.Should().ThrowAsync<DomainRuleException>();
    }
}
