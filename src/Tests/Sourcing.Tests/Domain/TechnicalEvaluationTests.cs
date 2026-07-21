using FSH.Modules.Sourcing.Domain;

namespace Sourcing.Tests.Domain;

public sealed class TechnicalEvaluationTests
{
    [Fact]
    public void WeightedForEvaluator_Should_ReturnNull_When_CriterionMissing()
    {
        var scores = new[]
        {
            TechnicalScore.Create(Guid.NewGuid(), Guid.NewGuid(), "ev1", TechnicalCriterion.Compliance, 80),
            TechnicalScore.Create(Guid.NewGuid(), Guid.NewGuid(), "ev1", TechnicalCriterion.Experience, 80),
        };

        TechnicalEvaluation.WeightedForEvaluator(scores).ShouldBeNull();
    }

    [Fact]
    public void WeightedForEvaluator_Should_ApplyFixedWeights()
    {
        var rfqId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        // 100 across all criteria → weighted = 100
        var scores = AllCriteria(rfqId, vendorId, "ev1", 100);

        TechnicalEvaluation.WeightedForEvaluator(scores).ShouldBe(100m);
    }

    [Fact]
    public void Committee_Should_Average_CompleteEvaluators()
    {
        var rfqId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var scores = AllCriteria(rfqId, vendorId, "ev1", 80)
            .Concat(AllCriteria(rfqId, vendorId, "ev2", 100))
            .ToList();

        TechnicalEvaluation.Committee(scores).ShouldBe(90m);
    }

    [Fact]
    public void Pass_Should_UseThreshold70()
    {
        TechnicalEvaluation.Pass(69.9m).ShouldBeFalse();
        TechnicalEvaluation.Pass(70m).ShouldBeTrue();
        TechnicalEvaluation.Pass(null).ShouldBeFalse();
    }

    [Fact]
    public void Alias_Should_MapInviteOrder_ToBidderLetters()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var order = new[] { a, b };

        TechnicalEvaluation.Alias(order, a).ShouldBe("Bidder A");
        TechnicalEvaluation.Alias(order, b).ShouldBe("Bidder B");
        TechnicalEvaluation.Alias(order, Guid.NewGuid()).ShouldBe("Bidder ?");
    }

    [Fact]
    public void AwardEligibility_DualFinalized_Should_RequirePass()
    {
        var rfq = OpenDualFinalized();

        AwardEligibility.IsEligible(rfq, submitted: true, technicallyPassed: false).ShouldBeFalse();
        AwardEligibility.IsEligible(rfq, submitted: true, technicallyPassed: true).ShouldBeTrue();
        AwardEligibility.IsEligible(rfq, submitted: false, technicallyPassed: true).ShouldBeFalse();
    }

    private static List<TechnicalScore> AllCriteria(Guid rfqId, Guid vendorId, string evaluatorId, int score) =>
    [
        TechnicalScore.Create(rfqId, vendorId, evaluatorId, TechnicalCriterion.Compliance, score),
        TechnicalScore.Create(rfqId, vendorId, evaluatorId, TechnicalCriterion.Experience, score),
        TechnicalScore.Create(rfqId, vendorId, evaluatorId, TechnicalCriterion.Delivery, score),
        TechnicalScore.Create(rfqId, vendorId, evaluatorId, TechnicalCriterion.QA, score),
    ];

    private static Rfq OpenDualFinalized()
    {
        var rfq = Rfq.CreateDraft(
            "RFQ-1", "t", RfqEnvelope.Dual, "MYR", "o", ["PR-1"],
            [new RfqLine("L1", "ITEM-1", "w", 1m, "EA", null, null)]);
        rfq.UpdateDraft("t", RfqEnvelope.Dual, "MYR", null, DateTime.UtcNow.AddDays(3),
            [new RfqLine("L1", "ITEM-1", "w", 1m, "EA", null, null)], [], [], [], [], []);
        rfq.InviteVendor(Guid.NewGuid(), DateTime.UtcNow, 72, "buyer");
        rfq.MarkReleased(DateTime.UtcNow, "buyer");
        rfq.CloseEarly(DateTime.UtcNow, "buyer");
        rfq.OpenTechnicalEnvelope();
        rfq.FinalizeTechnical(true, true);
        return rfq;
    }
}
