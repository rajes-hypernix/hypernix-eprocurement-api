using eProcure.Application;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Identity;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class EvaluationServiceTests
{

    private static async Task<(EvaluationService Svc, TestContext C, Guid RfqId)> SetupAsync(
        bool techOpened = true, bool finalized = false, bool scored = true)
    {
        var c = TestContext.New();
        c.User.Roles = [Roles.Buyer];

        var rfq = new Rfq
        {
            Code = "RFQ-2026-0079", Title = "Pumps", Envelope = RfqEnvelope.Dual,
            Status = RfqStatus.Evaluation, TechnicalOpened = techOpened, TechFinalized = finalized,
            TechnicalEvaluatorIds = ["u_hafiz", "u_nur"],
            CommercialEvaluatorIds = ["u_tan"],
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        // Vendor Ids are generated at construction, so seed invitations/bids/scores with the real ids
        // from the start (no post-hoc re-point — a Restrict FK can't have its collection severed).
        var alpha = new eProcure.Domain.Suppliers.Vendor { Code = "V-A", Name = "Alpha Engineering", RegisteredName = "Alpha Engineering" };
        var beta = new eProcure.Domain.Suppliers.Vendor { Code = "V-B", Name = "Beta Works", RegisteredName = "Beta Works" };
        c.Db.Vendors.AddRange(alpha, beta);
        rfq.WithInvites(c.Clock.UtcNow, alpha.Id, beta.Id);
        c.Db.Rfqs.Add(rfq);
        c.Db.Bids.AddRange(
            new Bid { Code = "BID-A", RfqId = rfq.Id, VendorId = alpha.Id, Submitted = true, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow },
            new Bid { Code = "BID-B", RfqId = rfq.Id, VendorId = beta.Id, Submitted = true, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow });

        if (scored)
        {
            foreach (var ev in rfq.TechnicalEvaluatorIds)
                foreach (var crit in TechnicalCriteria.All)
                {
                    c.Db.TechnicalScores.Add(new TechnicalScore(rfq.Id, alpha.Id, ev, crit.Key, 88));
                    c.Db.TechnicalScores.Add(new TechnicalScore(rfq.Id, beta.Id, ev, crit.Key, 58));
                }
        }
        await c.Db.SaveChangesAsync();

        return (new EvaluationService(c.Db, c.Clock, c.Audit, c.User), c, rfq.Id);
    }

    [Fact]
    public async Task TechnicalEval_ForBuyer_ShowsRealVendorNames()
    {
        var (svc, c, rfqId) = await SetupAsync();
        c.User.Roles = [Roles.Buyer];
        var dto = await svc.GetTechnicalEvalAsync(rfqId);
        dto!.Masked.Should().BeFalse();
        dto.Vendors.Select(v => v.DisplayName).Should().Contain("Alpha Engineering");
    }

    [Fact]
    public async Task TechnicalEval_ForEvaluator_MasksIdentities_AndCannotBeDefeated()
    {
        var (svc, c, rfqId) = await SetupAsync();
        // Same data, but the principal is an evaluator → identities masked server-side.
        c.User.Roles = [Roles.TechEvaluator];
        var dto = await svc.GetTechnicalEvalAsync(rfqId);

        dto!.Masked.Should().BeTrue();
        dto.Vendors.Select(v => v.DisplayName).Should().OnlyContain(n => n.StartsWith("Bidder "));
        dto.Vendors.Select(v => v.DisplayName).Should().NotContain("Alpha Engineering");
    }

    [Fact]
    public async Task Finalize_ComputesCommitteePassFail_MegatechStyleFail()
    {
        var (svc, c, rfqId) = await SetupAsync(finalized: false);
        var dto = await svc.FinalizeTechnicalAsync(rfqId);

        dto.Finalized.Should().BeTrue();
        var alpha = dto.Vendors.Single(v => Math.Abs((v.Committee ?? 0) - 88) < 0.01);
        var beta = dto.Vendors.Single(v => Math.Abs((v.Committee ?? 0) - 58) < 0.01);
        alpha.Pass.Should().BeTrue();
        beta.Pass.Should().BeFalse();   // 58 < 70 threshold
        (await c.Db.AuditEntries.AnyAsync(a => a.Action == "Technical finalized")).Should().BeTrue();
    }

    [Fact]
    public async Task OpenCommercial_BeforeTechnicalFinalized_IsRejected_SealedBids()
    {
        var (svc, _, rfqId) = await SetupAsync(finalized: false);
        var act = () => svc.OpenCommercialAsync(rfqId);
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*Finalize technical*");
    }

    [Fact]
    public async Task OpenCommercial_AfterFinalized_ByCommEvaluator_Succeeds()
    {
        var (svc, c, rfqId) = await SetupAsync(finalized: true);
        c.User.UserId = "u_tan"; c.User.Roles = [Roles.CommEvaluator];   // assigned commercial evaluator
        var dto = await svc.OpenCommercialAsync(rfqId);
        dto.CommercialOpened.Should().BeTrue();
    }

    [Fact]
    public async Task OpenCommercial_ByNonAssignedEvaluator_IsForbidden()
    {
        var (svc, c, rfqId) = await SetupAsync(finalized: true);
        c.User.UserId = "u_faridah"; c.User.Roles = [Roles.Buyer];   // not a commercial evaluator
        var act = () => svc.OpenCommercialAsync(rfqId);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task OpenTechnical_ByNonAssignedEvaluator_IsForbidden()
    {
        var (svc, c, rfqId) = await SetupAsync(techOpened: false, finalized: false);
        c.User.UserId = "u_faridah"; c.User.Roles = [Roles.Buyer];   // not a technical evaluator
        var act = () => svc.OpenTechnicalAsync(rfqId);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task SetScore_AfterFinalize_IsRejected()
    {
        var (svc, c, rfqId) = await SetupAsync(finalized: true);
        var vid = (await c.Db.Vendors.FirstAsync(v => v.Code == "V-A")).Id;
        var act = () => svc.SetScoreAsync(rfqId, new SetScoreRequest(vid, "u_hafiz", "compliance", 90));
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*finalized*");
    }

    [Fact]
    public async Task Score_ByVendorPrincipal_IsForbidden()
    {
        var (svc, c, rfqId) = await SetupAsync();
        c.User.Roles = [Roles.Vendor];
        c.User.VendorId = Guid.NewGuid();
        var vid = (await c.Db.Vendors.FirstAsync(v => v.Code == "V-A")).Id;
        var act = () => svc.SetScoreAsync(rfqId, new SetScoreRequest(vid, "u_hafiz", "compliance", 90));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task TechnicalEval_exposes_questions_and_each_vendor_answer()
    {
        var (svc, c, rfqId) = await SetupAsync();
        var rfq = await c.Db.Rfqs.FirstAsync(r => r.Id == rfqId);
        rfq.FormItems.Add(new FormItem { Kind = "question", Group = "technical", Label = "ISO certifications held", Type = "short_text", Order = 4 });
        var alpha = await c.Db.Vendors.FirstAsync(v => v.Code == "V-A");
        var bidA = await c.Db.Bids.FirstAsync(b => b.VendorId == alpha.Id);
        bidA.Answers.Add(new BidAnswer { QuestionOrder = 4, Value = "ISO 9001:2015" });
        await c.Db.SaveChangesAsync();

        var dto = await svc.GetTechnicalEvalAsync(rfqId);
        dto!.TechnicalQuestions.Should().ContainSingle().Which.Label.Should().Be("ISO certifications held");
        var alphaScore = dto.Vendors.First(v => v.VendorId == alpha.Id);
        alphaScore.Answers.Should().ContainSingle(a => a.QuestionOrder == 4 && a.Value == "ISO 9001:2015");
    }
}
