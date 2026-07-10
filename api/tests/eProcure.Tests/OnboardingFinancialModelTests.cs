using eProcure.Domain.Onboarding;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice A — the Altman Z′ financial model (EDGE-CASES B3–B5). Pure-domain tests pinning the model
/// to the prototype's exact output for its seed vectors, the band/score rules at the boundaries, and
/// snapshot-vs-live immutability. Test vectors were computed from the prototype's own
/// altmanZ/weighted/scoreFor with identical inputs (FIN_C → Z 1.258645, C, 28; FIN_D → 0.729867, D, 16).
/// </summary>
public class OnboardingFinancialModelTests
{
    // ---- B3: correctness against the prototype's seed vectors ----
    [Fact]
    public void FinC_HealthyVector_MatchesPrototype()
    {
        var years = OnboardingTestData.FinC();

        AltmanZModel.Z(years[0]).Should().BeApproximately(1.476064, 1e-6);
        AltmanZModel.Z(years[1]).Should().BeApproximately(1.276400, 1e-6);
        AltmanZModel.Z(years[2]).Should().BeApproximately(1.161025, 1e-6);

        var z = AltmanZModel.Weighted(years);
        z.Should().BeApproximately(1.258645, 1e-6);
        AltmanZModel.BandFor(z).Should().Be(FinancialBand.C);
        AltmanZModel.RiskFor(FinancialBand.C).Should().Be(RiskCategory.Medium);
        AltmanZModel.ScoreFor(z).Should().Be(28);
    }

    [Fact]
    public void FinD_DistressedVector_MatchesPrototype()
    {
        var years = OnboardingTestData.FinD();

        var z = AltmanZModel.Weighted(years);
        z.Should().BeApproximately(0.729867, 1e-6);
        AltmanZModel.BandFor(z).Should().Be(FinancialBand.D);
        AltmanZModel.RiskFor(FinancialBand.D).Should().Be(RiskCategory.High);
        AltmanZModel.ScoreFor(z).Should().Be(16);
    }

    // ---- B4: band thresholds at the boundaries (A ≥2.9, B ≥2.0, C ≥1.23, else D) ----
    [Theory]
    [InlineData(2.9, FinancialBand.A)]
    [InlineData(2.89, FinancialBand.B)]
    [InlineData(2.0, FinancialBand.B)]
    [InlineData(1.99, FinancialBand.C)]
    [InlineData(1.23, FinancialBand.C)]
    [InlineData(1.229, FinancialBand.D)]
    [InlineData(0.0, FinancialBand.D)]
    public void BandFor_HonoursThresholds(double z, FinancialBand expected) =>
        AltmanZModel.BandFor(z).Should().Be(expected);

    // ---- B4: score = clamp(round(z/4.5·100), 5, 99), JS Math.round semantics ----
    [Theory]
    [InlineData(1.258645, 28)]   // FIN_C
    [InlineData(0.729867, 16)]   // FIN_D
    [InlineData(4.5, 99)]        // 100 clamped to 99
    [InlineData(10.0, 99)]       // clamp high
    [InlineData(0.1, 5)]         // 2 -> clamped up to 5
    [InlineData(-1.0, 5)]        // negative clamped to 5
    public void ScoreFor_RoundsAndClamps(double z, int expected) =>
        AltmanZModel.ScoreFor(z).Should().Be(expected);

    // ---- B5: the stored snapshot is the record of decision; live values are derived ----
    [Fact]
    public void Snapshot_IsFrozen_WhileLiveRecomputes()
    {
        var assessment = new VendorFinancialAssessment(Guid.NewGuid(), OnboardingTestData.FinC(), OnboardingTestData.T);
        var snap = assessment.CaptureSnapshot(FinancialSnapshotStage.AtSubmit, OnboardingTestData.T);

        snap.Band.Should().Be(FinancialBand.C);
        snap.Score.Should().Be(28);
        snap.WeightedZ.Should().BeApproximately(1.258645, 1e-6);

        // A later edit to the figures changes the LIVE value but never the captured snapshot.
        assessment.Years.Single(y => y.YearIndex == 2).Revenue += 50000;
        assessment.LiveWeightedZ.Should().NotBeApproximately(snap.WeightedZ, 1e-3);
        snap.WeightedZ.Should().BeApproximately(1.258645, 1e-6);   // unchanged
        assessment.Snapshots.Should().ContainSingle();
    }

    [Fact]
    public void StatementFor_ReturnsBandSpecificWording()
    {
        AltmanZModel.StatementFor(FinancialBand.A).Should().Contain("without financial conditions");
        AltmanZModel.StatementFor(FinancialBand.D).Should().Contain("distress zone");
    }

    // ---- guard: zero denominators must yield a FINITE Z (band D / score 5), never NaN/Infinity ----
    [Fact]
    public void ZeroFigures_ProduceFiniteBandD_NeverNaN()
    {
        var years = new[] { new FinancialYearFigures(0), new FinancialYearFigures(1), new FinancialYearFigures(2) }; // all zeros
        var z = AltmanZModel.Weighted(years);

        double.IsFinite(z).Should().BeTrue();
        z.Should().Be(0);
        AltmanZModel.BandFor(z).Should().Be(FinancialBand.D);
        AltmanZModel.ScoreFor(z).Should().Be(5);
        AltmanZModel.ScoreFor(double.NaN).Should().Be(5);            // defensive clamp
        AltmanZModel.ScoreFor(double.PositiveInfinity).Should().Be(5);
    }

    // ---- guard: an out-of-range YearIndex contributes nothing instead of throwing ----
    [Fact]
    public void OutOfRangeYearIndex_IsIgnored_NotThrown()
    {
        var rogue = new[] { new FinancialYearFigures(7) { TotalAssets = 1000, Revenue = 5000 } };
        var act = () => AltmanZModel.Weighted(rogue);
        act.Should().NotThrow();
        AltmanZModel.Weighted(rogue).Should().Be(0);
    }
}
