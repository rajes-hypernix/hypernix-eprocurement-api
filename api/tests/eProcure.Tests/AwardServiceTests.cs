using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Identity;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class AwardServiceTests
{
    private sealed class NoopNetSuite : INetSuiteClient
    {
        public Task PushPurchaseOrderAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushVendorBillAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushItemReceiptAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushBillPaymentAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
    }

    // Dual envelope, technically finalized: VA passes, VB fails. Both bid both lines.
    private static async Task<(AwardService Svc, TestContext C, Guid RfqId, Guid VA, Guid VB)> SetupAsync()
    {
        var c = TestContext.New(actorId: "u_faridah", actorName: "Faridah");
        c.User.Roles = [Roles.Buyer];

        var va = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha" };
        var vb = new Vendor { Code = "V-B", Name = "Beta", RegisteredName = "Beta" };
        c.Db.Vendors.AddRange(va, vb);
        await c.Db.SaveChangesAsync();

        var rfq = new Rfq
        {
            Code = "RFQ-2026-0079", Title = "Pumps", Envelope = RfqEnvelope.Dual,
            TechnicalOpened = true, TechFinalized = true, CommercialOpened = true,
            TechnicalEvaluatorIds = ["u_hafiz"],
            Lines = [new RfqLine { ItemCode = "PUMP", Description = "Pump", Qty = 4, Uom = "Unit" }],
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        }.SeededAs(RfqStatus.Evaluation);
        rfq.WithInvites(c.Clock.UtcNow, va.Id, vb.Id);
        c.Db.Rfqs.Add(rfq);
        c.Db.Bids.AddRange(
            new Bid { Code = "BID-A", RfqId = rfq.Id, VendorId = va.Id, Submitted = true, Lines = [new BidLine { ItemCode = "PUMP", Bidding = true, Price = 100, Qty = 4 }], CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow },
            new Bid { Code = "BID-B", RfqId = rfq.Id, VendorId = vb.Id, Submitted = true, Lines = [new BidLine { ItemCode = "PUMP", Bidding = true, Price = 90, Qty = 4 }], CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow });
        // VA passes (88), VB fails (58)
        foreach (var crit in TechnicalCriteria.All)
        {
            c.Db.TechnicalScores.Add(new TechnicalScore(rfq.Id, va.Id, "u_hafiz", crit.Key, 88));
            c.Db.TechnicalScores.Add(new TechnicalScore(rfq.Id, vb.Id, "u_hafiz", crit.Key, 58));
        }
        await c.Db.SaveChangesAsync();

        var svc = new AwardService(c.Db, c.Clock, c.Codes, c.Audit, new NoopNetSuite(), c.User);
        return (svc, c, rfq.Id, va.Id, vb.Id);
    }

    [Fact]
    public async Task Eligibility_DualFinalized_ExcludesFailedVendor()
    {
        var (svc, _, rfqId, va, vb) = await SetupAsync();
        var elig = await svc.GetEligibilityAsync(rfqId);
        var options = elig!.Lines.Single().Options.Select(o => o.VendorId).ToList();
        options.Should().Contain(va);
        options.Should().NotContain(vb);   // failed technical → not awardable
    }

    [Fact]
    public async Task Submit_ToFailedVendor_IsRejected_Eligibility()
    {
        var (svc, _, rfqId, _, vb) = await SetupAsync();
        var act = () => svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", vb, 4)]));
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*did not bid or failed*");
    }

    [Fact]
    public async Task Submit_OverCap_IsRejected_QtyCap()
    {
        var (svc, _, rfqId, va, _) = await SetupAsync();
        // required 4, offered 4 → cap 4; allocate 5
        var act = () => svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 5)]));
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*exceeds the cap*");
    }

    [Fact]
    public async Task Submit_CreatesPendingApproval_AndAudits()
    {
        var (svc, c, rfqId, va, _) = await SetupAsync();
        var dto = await svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 4)]));
        dto.Status.Should().Be("PendingApproval");
        dto.TotalValue.Should().Be(400m);
        (await c.Db.AuditEntries.AnyAsync(a => a.EntityType == "Award" && a.Action == "Submitted for approval")).Should().BeTrue();
    }

    [Fact]
    public async Task Approve_BySameCreator_IsRejected_SoD()
    {
        var (svc, c, rfqId, va, _) = await SetupAsync();
        var dto = await svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 4)]));
        // same user (Faridah, also given Approver) tries to approve their own award
        c.User.Roles = [Roles.Buyer, Roles.Approver];
        var act = () => svc.ApproveAsync(dto.Id);
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*different person*");
    }

    [Fact]
    public async Task Approve_ByNonApproverRole_IsRejected_DoA()
    {
        var (svc, c, rfqId, va, _) = await SetupAsync();
        var dto = await svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 4)]));
        // different user but lacks the Approver role
        c.User.UserId = "u_other"; c.User.Roles = [Roles.Buyer];
        var act = () => svc.ApproveAsync(dto.Id);
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*Approver*");
    }

    [Fact]
    public async Task Approve_ByDifferentApprover_GeneratesOnePoPerVendor_AndAwardsRfq()
    {
        var (svc, c, rfqId, va, _) = await SetupAsync();
        var dto = await svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 4)]));

        c.User.UserId = "u_lim"; c.User.UserName = "Lim"; c.User.Roles = [Roles.Buyer, Roles.Approver];
        var approved = await svc.ApproveAsync(dto.Id);

        approved.Status.Should().Be("Approved");
        approved.ApproverUserId.Should().Be("u_lim");
        approved.PoCodes.Should().HaveCount(1);              // one PO per awarded vendor
        var po = await c.Db.PurchaseOrders.Include(p => p.Lines).SingleAsync();
        po.VendorId.Should().Be(va);
        po.Lines.Single().Qty.Should().Be(4);
        (await c.Db.Rfqs.FirstAsync(r => r.Id == rfqId)).Status.Should().Be(RfqStatus.Awarded);
    }

    [Fact]
    public async Task Eligibility_BeforeCommercialOpened_HidesPricing_SealedBids()
    {
        var (svc, c, rfqId, _, _) = await SetupAsync();
        var rfq = await c.Db.Rfqs.FirstAsync(r => r.Id == rfqId);
        rfq.CommercialOpened = false;   // re-seal commercial
        await c.Db.SaveChangesAsync();

        var elig = await svc.GetEligibilityAsync(rfqId);
        elig!.CommercialRevealed.Should().BeFalse();
        elig.Lines.SelectMany(l => l.Options).Should().BeEmpty();   // no prices leaked
    }
}
