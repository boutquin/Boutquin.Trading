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
/// Cross-language verification for the M-8 overfitting-corrected Sharpe analytics
/// (<see cref="ProbabilisticSharpeRatio"/> + <see cref="DeflatedSharpeRatio"/>) against the
/// own-formula Python reference in generate_deflated_sharpe_vectors.py.
///
/// Precision tier: PrecisionStatistical (1e-4). The C# engine evaluates the normal CDF /
/// inverse-CDF via Boutquin.Numerics' Abramowitz-Stegun / rational approximations, while the
/// Python reference uses scipy's near-exact routines — the same approximation-vs-exact gap
/// documented for the denoised covariance vectors.
/// </summary>
public sealed class DeflatedSharpeRatioCrossLanguageTests : CrossLanguageVerificationBase
{
    [Fact]
    public void WorkedExample_MatchesPythonReference_AndReproducesDsr0p9004()
    {
        using var doc = LoadVector("deflated_sharpe_worked_example");
        var root = doc.RootElement;

        var numTrials = root.GetProperty("num_trials").GetInt32();
        var vAnn = GetDecimal(root, "variance_of_trial_sharpes_annualized");
        var t = root.GetProperty("num_observations").GetInt32();
        var srAnn = GetDecimal(root, "annualized_sharpe");
        var skew = GetDecimal(root, "skewness");
        var fullKurt = GetDecimal(root, "full_kurtosis");
        var freq = root.GetProperty("trading_days_per_year").GetInt32();

        var sr0 = DeflatedSharpeRatio.ExpectedMaxSharpeRatio(numTrials, vAnn / freq);
        AssertWithinTolerance(sr0, GetDecimal(root, "expected_max_sharpe_non_annualized"),
            PrecisionStatistical, "ŜR₀: ");

        var dsr = DeflatedSharpeRatio.Evaluate(srAnn, numTrials, vAnn, t, skew, fullKurt, freq);
        AssertWithinTolerance(dsr, GetDecimal(root, "dsr"), PrecisionStatistical, "DSR: ");

        // Independent anchor: the paper's hand-computed DSR ≈ 0.9004.
        AssertWithinTolerance(dsr, 0.9004m, 1e-3m, "DSR vs paper 0.9004: ");

        // Material-trap witness: the excess-as-full value inflates the DSR above the correct one.
        var excessAsFull = DeflatedSharpeRatio.Evaluate(srAnn, numTrials, vAnn, t, skew, 7m, freq);
        AssertWithinTolerance(excessAsFull, GetDecimal(root, "dsr_excess_kurtosis_as_full"),
            PrecisionStatistical, "DSR(excess-as-full): ");
        Assert.True(excessAsFull > dsr, "Excess-kurtosis-as-full must inflate the DSR.");
    }

    [Fact]
    public void GaussianPoint_MatchesPythonReference()
    {
        using var doc = LoadVector("deflated_sharpe_gaussian_point");
        var root = doc.RootElement;

        var dsr = DeflatedSharpeRatio.Evaluate(
            GetDecimal(root, "annualized_sharpe"),
            root.GetProperty("num_trials").GetInt32(),
            GetDecimal(root, "variance_of_trial_sharpes_annualized"),
            root.GetProperty("num_observations").GetInt32(),
            GetDecimal(root, "skewness"),
            GetDecimal(root, "full_kurtosis"),
            root.GetProperty("trading_days_per_year").GetInt32());

        AssertWithinTolerance(dsr, GetDecimal(root, "dsr"), PrecisionStatistical, "Gaussian DSR: ");
        AssertWithinTolerance(dsr, 0.9505m, 1e-3m, "DSR vs paper 0.9505: ");
    }

    [Fact]
    public void ProbabilisticSharpeCases_MatchPythonReference()
    {
        using var doc = LoadVector("probabilistic_sharpe_cases");
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var psr = ProbabilisticSharpeRatio.Evaluate(
                GetDecimal(c, "observed_sharpe"),
                GetDecimal(c, "benchmark_sharpe"),
                c.GetProperty("num_observations").GetInt32(),
                GetDecimal(c, "skewness"),
                GetDecimal(c, "full_kurtosis"));

            AssertWithinTolerance(psr, GetDecimal(c, "psr"), PrecisionStatistical, "PSR: ");
        }
    }

    [Fact]
    public void DeflatedSharpeMonotoneInN_MatchesPythonReference()
    {
        using var doc = LoadVector("deflated_sharpe_monotone_n");
        var root = doc.RootElement;

        var vAnn = GetDecimal(root, "variance_of_trial_sharpes_annualized");
        var t = root.GetProperty("num_observations").GetInt32();
        var srAnn = GetDecimal(root, "annualized_sharpe");
        var skew = GetDecimal(root, "skewness");
        var fullKurt = GetDecimal(root, "full_kurtosis");
        var freq = root.GetProperty("trading_days_per_year").GetInt32();

        foreach (var point in root.GetProperty("series").EnumerateArray())
        {
            var n = point.GetProperty("num_trials").GetInt32();
            var dsr = DeflatedSharpeRatio.Evaluate(srAnn, n, vAnn, t, skew, fullKurt, freq);
            AssertWithinTolerance(dsr, GetDecimal(point, "dsr"), PrecisionStatistical, $"DSR(N={n}): ");
        }
    }
}
