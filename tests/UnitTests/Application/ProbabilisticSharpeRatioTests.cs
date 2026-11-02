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

namespace Boutquin.Trading.Tests.UnitTests.Application;

using Boutquin.Trading.Application.Analytics;

/// <summary>
/// Fidelity + degenerate-input tests for <see cref="ProbabilisticSharpeRatio"/>,
/// the Bailey &amp; López de Prado (2014) PSR analytic (Eq. 2, p. 8).
/// </summary>
public sealed class ProbabilisticSharpeRatioTests
{
    // Non-annualized observed Sharpe used across the analytic-property tests.
    private const decimal Sr = 0.15811388m;   // 2.5 / sqrt(250)
    private const int T = 1250;
    private const decimal FullKurtosisNormal = 3m;

    [Fact]
    public void Evaluate_BenchmarkEqualsObserved_ReturnsExactlyOneHalf()
    {
        // PSR(SR* = ŜR) = Z[0] = 0.5 — the numerator vanishes.
        var psr = ProbabilisticSharpeRatio.Evaluate(Sr, Sr, T, skewness: 0m, kurtosis: FullKurtosisNormal);

        psr.Should().BeApproximately(0.5m, 1e-9m);
    }

    [Fact]
    public void Evaluate_AcrossRange_IsStrictlyInsideOpenUnitInterval()
    {
        // Standard-normal CDF codomain: PSR ∈ (0,1). Benchmarks are kept near the observed
        // Sharpe so the CDF argument stays in the non-saturating range — Φ(z) collapses to
        // exactly 0 or 1 in double precision only for |z| ≳ 8, a float artifact, not an
        // analytic violation of the open-interval codomain.
        foreach (var benchmark in new[] { 0.05m, 0.10m, 0.14m, 0.158m, 0.17m, 0.20m, 0.25m })
        {
            var psr = ProbabilisticSharpeRatio.Evaluate(Sr, benchmark, T, skewness: -0.5m, kurtosis: 4m);
            psr.Should().BeGreaterThan(0m);
            psr.Should().BeLessThan(1m);
        }
    }

    [Fact]
    public void Evaluate_NegativeSkewAndFatterTails_LowerPsr()
    {
        // Paper p. 8: for a given SR*, PSR increases with positive skew and decreases with fatter tails.
        var baseline = ProbabilisticSharpeRatio.Evaluate(Sr, 0.05m, T, skewness: 0m, kurtosis: 3m);
        var negativeSkew = ProbabilisticSharpeRatio.Evaluate(Sr, 0.05m, T, skewness: -1m, kurtosis: 3m);
        var fatterTails = ProbabilisticSharpeRatio.Evaluate(Sr, 0.05m, T, skewness: 0m, kurtosis: 8m);

        negativeSkew.Should().BeLessThan(baseline);
        fatterTails.Should().BeLessThan(baseline);
    }

    [Fact]
    public void Evaluate_TooFewObservations_Throws()
    {
        var act = () => ProbabilisticSharpeRatio.Evaluate(Sr, 0m, numObservations: 1, skewness: 0m, kurtosis: 3m);
        act.Should().Throw<InsufficientDataException>();
    }

    [Fact]
    public void Evaluate_NonPositiveDenominator_ThrowsCalculationException()
    {
        // Extreme positive skew with a large Sharpe drives 1 − γ₃·ŜR + ((γ₄−1)/4)·ŜR² ≤ 0.
        var act = () => ProbabilisticSharpeRatio.Evaluate(
            observedSharpe: 5m, benchmarkSharpe: 0m, numObservations: T, skewness: 10m, kurtosis: 3m);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }
}
