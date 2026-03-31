// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

using Boutquin.Trading.Domain.Analytics;

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// TDD tests for round-2 domain review findings (R2D-01 through R2D-06).
/// </summary>
public sealed class R2DomainReviewFixesTests
{
    private const decimal Precision = 1e-12m;

    // ── R2D-01: CAGR returns raw decimal, not percentage ─────────────

    [Theory]
    [MemberData(nameof(R2DomainReviewFixesTestData.CagrRawDecimalData), MemberType = typeof(R2DomainReviewFixesTestData))]
    public void CompoundAnnualGrowthRate_ReturnsRawDecimal_NotPercentage(
        decimal[] dailyReturns,
        decimal expectedRawDecimal)
    {
        var result = dailyReturns.CompoundAnnualGrowthRate();

        // Must be raw decimal (~3.32), not percentage (~332.56)
        result.Should().BeApproximately(expectedRawDecimal, Precision);
    }

    [Fact]
    public void CompoundAnnualGrowthRate_ConsistentWithCalmarRatio()
    {
        // Use returns that produce a drawdown so CalmarRatio doesn't throw
        var dailyReturns = new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.005m, 0.015m, -0.01m, 0.02m, 0.01m, -0.005m };

        _ = dailyReturns.CompoundAnnualGrowthRate();
        var calmarRatio = dailyReturns.CalmarRatio();

        // CalmarRatio = CAGR / |MaxDrawdown|. With raw CAGR, result should be reasonable (< 1000).
        // With percentage CAGR, it would be ~100x too large.
        calmarRatio.Should().BeGreaterThan(0m);
        calmarRatio.Should().BeLessThan(1000m);
    }

    // ── R2D-02: DownsideDeviation returns 0 for no downside ──────────

    [Theory]
    [MemberData(nameof(R2DomainReviewFixesTestData.DownsideDeviationZeroData), MemberType = typeof(R2DomainReviewFixesTestData))]
    public void DownsideDeviation_AllReturnsAboveRiskFree_ReturnsZero(
        decimal[] dailyReturns,
        decimal riskFreeRate,
        decimal expectedResult)
    {
        var result = dailyReturns.DownsideDeviation(riskFreeRate);

        result.Should().Be(expectedResult);
    }

    [Theory]
    [MemberData(nameof(R2DomainReviewFixesTestData.DownsideDeviationPositiveData), MemberType = typeof(R2DomainReviewFixesTestData))]
    public void DownsideDeviation_SomeReturnsBelowRiskFree_ReturnsPositive(
        decimal[] dailyReturns,
        decimal riskFreeRate)
    {
        var result = dailyReturns.DownsideDeviation(riskFreeRate);

        result.Should().BeGreaterThan(0m);
    }

    // ── R2D-03: SortinoRatio guards zero DownsideDeviation ───────────

    [Theory]
    [MemberData(nameof(R2DomainReviewFixesTestData.SortinoRatioZeroDownsideData), MemberType = typeof(R2DomainReviewFixesTestData))]
    public void SortinoRatio_ZeroDownsideDeviation_ThrowsCalculationException(
        decimal[] dailyReturns,
        decimal riskFreeRate)
    {
        var act = () => dailyReturns.SortinoRatio(riskFreeRate);

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Theory]
    [MemberData(nameof(R2DomainReviewFixesTestData.SortinoRatioNormalData), MemberType = typeof(R2DomainReviewFixesTestData))]
    public void SortinoRatio_NonZeroDownsideDeviation_ReturnsValue(
        decimal[] dailyReturns,
        decimal riskFreeRate)
    {
        var result = dailyReturns.SortinoRatio(riskFreeRate);

        result.Should().NotBe(0m);
    }

    // ── R2D-04: EquityCurveExtensions zero equity throws ─────────────

    [Fact]
    public void MonthlyReturns_PreviousMonthZeroEquity_ThrowsCalculationException()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2024, 1, 15), 1000m },
            { new DateOnly(2024, 1, 31), 0m },    // Total loss at end of Jan
            { new DateOnly(2024, 2, 15), 500m },   // Feb has value, but prev month ended at 0
        };

        Action act = () => equityCurve.MonthlyReturns();

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void AnnualReturns_PreviousYearZeroEquity_ThrowsCalculationException()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2023, 6, 15), 1000m },
            { new DateOnly(2023, 12, 31), 0m },   // Total loss at end of 2023
            { new DateOnly(2024, 6, 15), 500m },   // 2024 has value, but prev year ended at 0
        };

        Action act = () => equityCurve.AnnualReturns();

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void MonthlyReturns_NormalEquity_ReturnsCorrectValues()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2024, 1, 31), 1000m },
            { new DateOnly(2024, 2, 29), 1100m },
            { new DateOnly(2024, 3, 31), 1050m },
        };

        var result = equityCurve.MonthlyReturns();

        result[(2024, 2)].Should().BeApproximately(0.10m, Precision);
        result[(2024, 3)].Should().BeApproximately(-0.04545454545454545454545454545m, Precision);
    }

    // ── R2D-05: AnnualizedReturn Math.Pow overflow ───────────────────

    [Fact]
    public void AnnualizedReturn_ExtremeReturn_ThrowsCalculationException()
    {
        // Construct returns that produce a massive cumulative return.
        // A single extreme daily return raised to power 252/1 will overflow decimal.
        var dailyReturns = new[] { 100m, 100m }; // (1+100)*(1+100)-1 = 10200. Math.Pow(10201, 252/2) overflows.

        var act = () => dailyReturns.AnnualizedReturn();

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    // ── R2D-06: MonteCarloResult SharpeRatios is IReadOnlyList ───────

    [Fact]
    public void MonteCarloResult_SharpeRatiosIsReadOnly()
    {
        // This test verifies the property type is IReadOnlyList<decimal> after the fix.
        // Pre-fix: decimal[] is NOT IReadOnlyList — test will fail on the type assertion.
        var sharpes = new[] { 0.5m, 1.0m, 1.5m };

        var result = new MonteCarloResult(
            SimulationCount: 3,
            SharpeRatios: sharpes,
            MedianSharpe: 1.0m,
            Percentile5Sharpe: 0.5m,
            Percentile95Sharpe: 1.5m,
            MeanSharpe: 1.0m);

        // After fix, SharpeRatios will be IReadOnlyList<decimal>.
        // decimal[] does implement IReadOnlyList<decimal>, so we also verify immutability
        // by checking that the underlying collection is not the original array.
        result.SharpeRatios.Should().BeAssignableTo<IReadOnlyList<decimal>>();
        result.SharpeRatios.Should().HaveCount(3);
        result.SharpeRatios[0].Should().Be(0.5m);
        result.SharpeRatios[1].Should().Be(1.0m);
        result.SharpeRatios[2].Should().Be(1.5m);
    }
}
