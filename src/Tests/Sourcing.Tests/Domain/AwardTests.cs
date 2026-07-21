using FSH.Modules.Sourcing.Domain;

namespace Sourcing.Tests.Domain;

public sealed class AwardTests
{
    [Fact]
    public void Create_Should_Throw_When_NoAllocations()
    {
        Should.Throw<SourcingRuleException>(() =>
            Award.Create("AWD-1", Guid.NewGuid(), "buyer-1", []));
    }

    [Fact]
    public void Approve_Should_Enforce_SegregationOfDuties()
    {
        var award = Award.Create(
            "AWD-1",
            Guid.NewGuid(),
            "buyer-1",
            [new AwardAllocation("L1", Guid.NewGuid(), 10m, 95m)]);

        Should.Throw<SourcingRuleException>(() => award.Approve("buyer-1", DateTime.UtcNow))
            .Message.ShouldContain("segregation of duties");
    }

    [Fact]
    public void Approve_Should_Succeed_When_DifferentApprover()
    {
        var award = Award.Create(
            "AWD-1",
            Guid.NewGuid(),
            "buyer-1",
            [new AwardAllocation("L1", Guid.NewGuid(), 10m, 95m)]);

        award.Approve("approver-1", DateTime.UtcNow);

        award.Status.ShouldBe(AwardStatus.Approved);
        award.ApproverUserId.ShouldBe("approver-1");
        award.TotalValue.ShouldBe(950m);
    }

    [Fact]
    public void MarkPendingApproval_Should_Throw_When_AlreadyApproved()
    {
        var award = Award.Create(
            "AWD-1",
            Guid.NewGuid(),
            "buyer-1",
            [new AwardAllocation("L1", Guid.NewGuid(), 1m, 10m)]);
        award.Approve("approver-1", DateTime.UtcNow);

        Should.Throw<SourcingRuleException>(() =>
            award.MarkPendingApproval([new AwardAllocation("L1", Guid.NewGuid(), 1m, 11m)]));
    }
}
