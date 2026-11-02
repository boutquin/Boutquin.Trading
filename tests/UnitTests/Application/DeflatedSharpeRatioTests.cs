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
/// Fidelity + degenerate-input tests for <see cref="DeflatedSharpeRatio"/>,
/// anchored to the Bailey &amp; López de Prado (2014) worked example (pp. 9–10).
/// </summary>
public sealed class DeflatedSharpeRatioTests
{
    // The paper's own worked example (pp. 9–10):
    //   N = 100 trials, V[{ŜR_n}] = 0.5 (annualized), T = 1250 observations,
    //   annualized ŜR = 2.5 (250 obs/yr), γ̂₃ = −3, γ̂₄ = 10 (FULL kurtosis).
    // ⟹ ŜR₀ ≈ 0.1132 (non-annualized), DSR ≈ 0.9004.
    private const int N = 100;
    private const decimal V = 0.5m;
    private const int T = 1250;
    private const decimal AnnualizedSr = 2.5m;
    private const decimal Skew = -3m;
    private const decimal FullKurtosis = 10m;
    private const int Freq = 250;

    [Fact]
    public void ExpectedMaxSharpeRatio_WorkedExample_ReproducesPaperValue()
    {
        // Non-annualized V = 0.5 / 250; the paper reports ŜR₀ ≈ 0.1132 non-annualized.
        var sr0 = DeflatedSharpeRatio.ExpectedMaxSharpeRatio(N, V / Freq);

        sr0.Should().BeApproximately(0.1132m, 5e-4m);
    }

    [Fact]
    public void Evaluate_WorkedExample_ReproducesDsr0p9004()
    {
        // THE fidelity anchor: reproduces DSR ≈ 0.9004 only with the correct annualization
        // AND full (not excess) kurtosis.
        var dsr = DeflatedSharpeRatio.Evaluate(
            AnnualizedSr, N, V, T, Skew, FullKurtosis, tradingDaysPerYear: Freq);

        dsr.Should().BeApproximately(0.9004m, 1e-3m);
    }

    [Fact]
    public void Evaluate_GaussianComparisonPoint_ReproducesDsr0p9505()
    {
        // Paper p. 10: "If the strategy had exhibited Normal returns (γ̂₃ = 0, γ̂₄ = 3),
        // DSR = 0.9505 after N = 88."
        var dsr = DeflatedSharpeRatio.Evaluate(
            AnnualizedSr, numTrials: 88, V, T, skewness: 0m, kurtosis: 3m, tradingDaysPerYear: Freq);

        dsr.Should().BeApproximately(0.9505m, 1e-3m);
    }

    [Fact]
    public void Evaluate_IsMonotonicallyNonIncreasingInNumTrials()
    {
        // Paper Exhibit 2: ŜR₀ increases with N, so DSR is non-increasing in N.
        decimal? previous = null;
        foreach (var n in new[] { 2, 5, 10, 50, 100, 500, 1000, 5000 })
        {
            var dsr = DeflatedSharpeRatio.Evaluate(
                AnnualizedSr, n, V, T, Skew, FullKurtosis, tradingDaysPerYear: Freq);

            if (previous is not null)
            {
                dsr.Should().BeLessThanOrEqualTo(previous.Value,
                    $"DSR must not increase as trials grow (N={n})");
            }

            previous = dsr;
        }
    }

    [Fact]
    public void Evaluate_ExcessKurtosisFedAsFull_InflatesDsr_ThePinForTheMaterialTrap()
    {
        // MATERIAL-TRAP PIN. DecimalArrayExtensions.Kurtosis() returns EXCESS kurtosis
        // (γ̂₄ − 3): the worked example's full γ̂₄ = 10 is excess 7. Feeding the excess
        // value 7 where the FULL value is required understates the denominator and
        // silently INFLATES the DSR — the opposite of the metric's purpose.
        var correctFull = DeflatedSharpeRatio.Evaluate(
            AnnualizedSr, N, V, T, Skew, kurtosis: 10m, tradingDaysPerYear: Freq);
        var excessAsFull = DeflatedSharpeRatio.Evaluate(
            AnnualizedSr, N, V, T, Skew, kurtosis: 7m, tradingDaysPerYear: Freq);

        correctFull.Should().BeApproximately(0.9004m, 1e-3m);
        excessAsFull.Should().BeGreaterThan(correctFull, "forgetting the +3 conversion inflates the DSR");
    }

    [Fact]
    public void FromReturns_AddsThreeToConvertExcessKurtosisToFull()
    {
        // Boundary pin: FromReturns derives moments from the returns array and MUST convert
        // returns.Kurtosis() (excess) to full kurtosis via +3 before the denominator.
        var returns = BuildReturns();
        var skew = returns.Skewness();
        var excessKurt = returns.Kurtosis();

        var fromReturns = DeflatedSharpeRatio.FromReturns(
            returns, AnnualizedSr, N, V, tradingDaysPerYear: Freq);

        var withConversion = DeflatedSharpeRatio.Evaluate(
            AnnualizedSr, N, V, returns.Length, skew, excessKurt + 3m, tradingDaysPerYear: Freq);
        var withoutConversion = DeflatedSharpeRatio.Evaluate(
            AnnualizedSr, N, V, returns.Length, skew, excessKurt, tradingDaysPerYear: Freq);

        fromReturns.Should().BeApproximately(withConversion, 1e-12m,
            "FromReturns must add 3 to the excess kurtosis");
        fromReturns.Should().NotBe(withoutConversion,
            "omitting the +3 conversion would give a materially different (inflated) DSR");
    }

    [Fact]
    public void ExpectedMaxSharpeRatio_TooFewTrials_Throws()
    {
        var act = () => DeflatedSharpeRatio.ExpectedMaxSharpeRatio(numTrials: 1, varianceOfTrialSharpes: 0.5m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExpectedMaxSharpeRatio_NegativeVariance_Throws()
    {
        var act = () => DeflatedSharpeRatio.ExpectedMaxSharpeRatio(numTrials: 10, varianceOfTrialSharpes: -0.1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Evaluate_NonPositiveTradingDays_Throws()
    {
        var act = () => DeflatedSharpeRatio.Evaluate(
            AnnualizedSr, N, V, T, Skew, FullKurtosis, tradingDaysPerYear: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static decimal[] BuildReturns()
    {
        // A skewed, fat-tailed synthetic series (deterministic) so Skewness/Kurtosis are non-trivial.
        var rng = new Random(7);
        var data = new decimal[600];
        for (var i = 0; i < data.Length; i++)
        {
            var u = rng.NextDouble();
            var v = rng.NextDouble();
            // Box-Muller then cube to fatten tails and skew.
            var z = Math.Sqrt(-2.0 * Math.Log(u + 1e-12)) * Math.Cos(2.0 * Math.PI * v);
            data[i] = (decimal)(0.0005 + 0.01 * z * Math.Abs(z));
        }

        return data;
    }
}
