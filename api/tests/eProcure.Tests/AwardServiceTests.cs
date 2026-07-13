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

        // AN-2: real Award→PO lineage is set at creation (not inferred from AwardCode later).
        po.AwardId.Should().Be(dto.Id, "the PO carries a real FK to the Award root");
        var alloc = await c.Db.Awards.AsNoTracking().Where(a => a.Id == dto.Id).SelectMany(a => a.Allocations).SingleAsync();
        po.Lines.Single().AwardAllocationId.Should().Be(alloc.Id, "each PO line links to the allocation it was cut from");
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

    // TEST-SWEEP-T2 (inventory PART 3): the one-award-per-RFQ constraint, pinned explicitly.
    [Fact]
    public async Task Second_award_submission_after_approval_is_rejected_one_award_per_rfq()
    {
        var (svc, c, rfqId, va, _) = await SetupAsync();
        var dto = await svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 4)]));
        c.User.UserId = "u_lim"; c.User.UserName = "Lim"; c.User.Roles = [Roles.Buyer, Roles.Approver];
        await svc.ApproveAsync(dto.Id);

        c.User.UserId = "u_faridah"; c.User.UserName = "Faridah"; c.User.Roles = [Roles.Buyer];
        var again = async () => await svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 1)]));
        (await again.Should().ThrowAsync<eProcure.Domain.DomainRuleException>())
            .WithMessage("*already been awarded*");
    }

    // ---- CF-FIX2-T3: shared fields carry PR→PO through provenance; ambiguity leaves a NOTE ----

    [Fact]
    public async Task Shared_field_value_carries_from_the_single_source_PR_to_the_generated_PO()
    {
        var (svc, c, rfqId, va, _) = await SetupAsync();
        // ONE source PR feeding the RFQ line (unambiguous provenance).
        var pr = new PurchaseRequisition { Code = "PR-CF3-1", Requestor = "F", CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
            Lines = { PrLine.Create("PUMP", "Pump", 4, "Unit", 100) } };
        c.Db.PurchaseRequisitions.Add(pr);
        await c.Db.SaveChangesAsync();
        c.Db.PrLineSourcings.Add(new PrLineSourcing(pr.Lines[0].Id, rfqId, "PUMP", 4m, c.Clock.UtcNow));

        // A def applied to BOTH Requisition and PurchaseOrder, with a value on the PR.
        var def = new eProcure.Domain.CustomFields.CustomFieldDef { Code = "custbody_carry", Label = "Carry", DataType = eProcure.Domain.CustomFields.CustomFieldDataType.Text, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        c.Db.CustomFieldDefs.Add(def);
        c.Db.CustomFieldDefApplications.AddRange(
            new eProcure.Domain.CustomFields.CustomFieldDefApplication { FieldDefId = def.Id, RecordType = eProcure.Domain.Views.RecordType.Requisition },
            new eProcure.Domain.CustomFields.CustomFieldDefApplication { FieldDefId = def.Id, RecordType = eProcure.Domain.Views.RecordType.PurchaseOrder });
        c.Db.CustomFieldValues.Add(new eProcure.Domain.CustomFields.CustomFieldValue
        {
            FieldDefId = def.Id, RecordType = eProcure.Domain.Views.RecordType.Requisition, RecordId = pr.Id,
            DataType = eProcure.Domain.CustomFields.CustomFieldDataType.Text, ValueText = "carried!", UpdatedUtc = c.Clock.UtcNow,
        });
        await c.Db.SaveChangesAsync();

        var dto = await svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 4)]));
        c.User.UserId = "u_lim"; c.User.UserName = "Lim"; c.User.Roles = [Roles.Buyer, Roles.Approver];
        await svc.ApproveAsync(dto.Id);

        var po = await c.Db.PurchaseOrders.SingleAsync();
        var carried = await c.Db.CustomFieldValues.SingleOrDefaultAsync(v =>
            v.FieldDefId == def.Id && v.RecordType == eProcure.Domain.Views.RecordType.PurchaseOrder && v.RecordId == po.Id);
        carried.Should().NotBeNull("the shared field's value carries PR→PO through the provenance chain");
        carried!.ValueText.Should().Be("carried!");
    }

    [Fact]
    public async Task Consolidated_provenance_skips_the_carry_with_a_VISIBLE_audit_note()
    {
        var (svc, c, rfqId, va, _) = await SetupAsync();
        // TWO source PRs feed the same RFQ line — ambiguous, must skip + note (operator ruling).
        foreach (var code in new[] { "PR-CF3-A", "PR-CF3-B" })
        {
            var pr = new PurchaseRequisition { Code = code, Requestor = "F", CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
                Lines = { PrLine.Create("PUMP", "Pump", 2, "Unit", 100) } };
            c.Db.PurchaseRequisitions.Add(pr);
            await c.Db.SaveChangesAsync();
            c.Db.PrLineSourcings.Add(new PrLineSourcing(pr.Lines[0].Id, rfqId, "PUMP", 2m, c.Clock.UtcNow));
        }
        var def = new eProcure.Domain.CustomFields.CustomFieldDef { Code = "custbody_skip", Label = "Skip", DataType = eProcure.Domain.CustomFields.CustomFieldDataType.Text, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        c.Db.CustomFieldDefs.Add(def);
        c.Db.CustomFieldDefApplications.AddRange(
            new eProcure.Domain.CustomFields.CustomFieldDefApplication { FieldDefId = def.Id, RecordType = eProcure.Domain.Views.RecordType.Requisition },
            new eProcure.Domain.CustomFields.CustomFieldDefApplication { FieldDefId = def.Id, RecordType = eProcure.Domain.Views.RecordType.PurchaseOrder });
        await c.Db.SaveChangesAsync();

        var dto = await svc.SubmitForApprovalAsync(rfqId, new SubmitAwardRequest([new AllocationInput("PUMP", va, 4)]));
        c.User.UserId = "u_lim"; c.User.UserName = "Lim"; c.User.Roles = [Roles.Buyer, Roles.Approver];
        await svc.ApproveAsync(dto.Id);

        var po = await c.Db.PurchaseOrders.SingleAsync();
        (await c.Db.CustomFieldValues.AnyAsync(v => v.RecordType == eProcure.Domain.Views.RecordType.PurchaseOrder))
            .Should().BeFalse("ambiguous provenance never guesses");
        var note = await c.Db.AuditEntries.SingleOrDefaultAsync(a => a.EntityId == po.Code && a.Action == "Custom fields not carried");
        note.Should().NotBeNull("the skip is a VISIBLE audit note on the PO (operator ruling)");
        note!.After.Should().Contain("2 source PRs");
    }
}
