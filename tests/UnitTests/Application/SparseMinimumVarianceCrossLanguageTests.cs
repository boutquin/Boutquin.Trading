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

using System.Text.Json;

using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification and fidelity tests for
/// <see cref="SparseMinimumVarianceConstruction"/> — Husmann, Shivarova &amp;
/// Steinert (2019), "Sparsity and Stability for Minimum-Variance Portfolios"
/// (arXiv 1910.11840), Model 2 (ℓ₁-budgeted LASSO min-var).
///
/// Validates the C# implementation against own-formula Python reference vectors
/// (tests/Verification/vectors/sparse_minvar_*.json, from
/// generate_sparse_minvar_vectors.py — NOT pypfopt/cvxpy/scikit-learn). The
/// augmented-QP optimum is replicated identically in both languages.
///
/// The defining fidelity reduction — <c>δ=∞ ⇒ standard min-var (§13)</c> — and the
/// long-only corner <c>δ=1</c> are asserted as solver-to-solver equivalences against
/// <see cref="MinimumVarianceConstruction"/> (both call ActiveSetQpSolver), so they
/// pin the production path without a Python boundary value.
/// </summary>
public sealed class SparseMinimumVarianceCrossLanguageTests : CrossLanguageVerificationBase
{
    // ─── Helpers ────────────────────────────────────────────────────────

    private static decimal[,] GetMatrix(JsonElement element)
    {
        var rows = element.EnumerateArray().ToArray();
        var n = rows.Length;
        var m = rows[0].EnumerateArray().Count();
        var result = new decimal[n, m];
        for (var i = 0; i < n; i++)
        {
            var cols = rows[i].EnumerateArray().ToArray();
            for (var j = 0; j < m; j++)
            {
                result[i, j] = (decimal)cols[j].GetDouble();
            }
        }

        return result;
    }

    private static IReadOnlyList<Symbol> MakeAssets(int n) =>
        Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    private static decimal[][] PlaceholderReturns(int n)
    {
        var placeholder = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            placeholder[i] = new[] { 0.01m, -0.01m };
        }

        return placeholder;
    }

    /// <summary>A covariance estimator that always returns a fixed matrix (test helper).</summary>
    private sealed class FixedCovarianceEstimator(decimal[,] fixedCov) : ICovarianceEstimator
    {
        public decimal[,] Estimate(decimal[][] returns) => (decimal[,])fixedCov.Clone();
    }

    // A real, well-conditioned interior 4-asset panel (sample covariance is interior:
    // the unconstrained min-var is strictly positive, so δ=1 and δ=∞ coincide there).
    private static decimal[][] RealInteriorPanel()
    {
        // 4 assets × 10 observations, low-correlation, all-positive min-var.
        return
        [
            new[] { 0.012m, -0.008m, 0.015m, -0.004m, 0.009m, -0.011m, 0.006m, 0.003m, -0.002m, 0.010m },
            new[] { -0.005m, 0.014m, -0.009m, 0.011m, -0.003m, 0.008m, -0.012m, 0.007m, 0.004m, -0.006m },
            new[] { 0.009m, 0.002m, -0.013m, 0.006m, 0.010m, -0.007m, 0.005m, -0.004m, 0.012m, -0.008m },
            new[] { -0.003m, 0.007m, 0.004m, -0.010m, 0.008m, 0.002m, -0.006m, 0.011m, -0.005m, 0.009m },
        ];
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-1: implements IPortfolioConstructionModel (weights ≥ 0, sum 1.0, empty→empty)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SparseMinimumVariance_IsPortfolioConstructionModel()
    {
        var model = new SparseMinimumVarianceConstruction();
        model.Should().BeAssignableTo<IPortfolioConstructionModel>();
    }

    [Fact]
    public void SparseMinimumVariance_EmptyAssets_ReturnsEmpty()
    {
        var model = new SparseMinimumVarianceConstruction();
        var result = model.ComputeTargetWeights([], []);
        result.Should().BeEmpty();
    }

    [Fact]
    public void SparseMinimumVariance_DefaultDelta_RealPanel_LongOnlyAndSumsToOne()
    {
        // AC-1 interface invariants on the DEFAULT model (δ=1 → long-only corner):
        // every weight ≥ 0 and the weights sum to 1.0.
        var returns = RealInteriorPanel();
        var assets = MakeAssets(returns.Length);

        var model = new SparseMinimumVarianceConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWithinTolerance(result.Values.Sum(), 1m, PrecisionNumeric, "Sparse weights sum: ");
        foreach (var (asset, w) in result)
        {
            Assert.True(w >= -PrecisionNumeric, $"Sparse weight {asset.Ticker} = {w} is negative");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-2: cross-language vectors — augmented-QP Model-2 LASSO own-formula
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SparseMinimumVariance_SlackBudget_MatchesPythonUnconstrainedMinVar()
    {
        // δ slack (5 ≥ gross≈1): optimum is the analytic unconstrained min-var,
        // reproduced cross-language to PrecisionNumeric.
        using var doc = LoadVector("sparse_minvar_interior_slack");
        var cov = GetMatrix(doc.RootElement.GetProperty("inputs").GetProperty("covariance"));
        var delta = (decimal)doc.RootElement.GetProperty("inputs").GetProperty("delta").GetDouble();
        var expected = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("weights"));

        var n = cov.GetLength(0);
        var assets = MakeAssets(n);
        var model = new SparseMinimumVarianceConstruction(new FixedCovarianceEstimator(cov), delta);
        var result = model.ComputeTargetWeights(assets, PlaceholderReturns(n));

        for (var i = 0; i < expected.Length; i++)
        {
            AssertWithinTolerance(result[assets[i]], expected[i], PrecisionNumeric,
                $"Sparse slack weight[{i}]: ");
        }
    }

    [Fact]
    public void SparseMinimumVariance_BindingBudget_MatchesPythonAugmentedQp()
    {
        // δ binding (1.3 < gross≈1.656): the ℓ₁ budget caps gross exposure; the
        // augmented-QP optimum is the two-equality KKT closed form, reproduced
        // cross-language to PrecisionNumeric.
        using var doc = LoadVector("sparse_minvar_binding_budget");
        var cov = GetMatrix(doc.RootElement.GetProperty("inputs").GetProperty("covariance"));
        var delta = (decimal)doc.RootElement.GetProperty("inputs").GetProperty("delta").GetDouble();
        var expected = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("weights"));
        var expectedGross = (decimal)doc.RootElement.GetProperty("expected").GetProperty("gross").GetDouble();

        var n = cov.GetLength(0);
        var assets = MakeAssets(n);
        var model = new SparseMinimumVarianceConstruction(new FixedCovarianceEstimator(cov), delta);
        var result = model.ComputeTargetWeights(assets, PlaceholderReturns(n));

        for (var i = 0; i < expected.Length; i++)
        {
            AssertWithinTolerance(result[assets[i]], expected[i], PrecisionNumeric,
                $"Sparse binding weight[{i}]: ");
        }

        // Sum-to-one always; gross hits the budget δ.
        AssertWithinTolerance(result.Values.Sum(), 1m, PrecisionNumeric, "Sparse binding sum: ");
        AssertWithinTolerance(result.Values.Select(Math.Abs).Sum(), expectedGross, PrecisionNumeric,
            "Sparse binding gross: ");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-3: FIDELITY — δ=∞ ⇒ standard min-var (§13); binding δ ⇒ different/sparser
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SparseMinimumVariance_InfiniteDelta_EqualsStandardMinVariance()
    {
        // THE reduction: with a non-binding budget (δ = decimal.MaxValue), SparseMinVar
        // must equal MinimumVarianceConstruction (§13) on a real interior panel, weight
        // for weight. Both are wired through the injected estimator; this pins the
        // augmented-QP collapse to the standard min-var.
        var returns = RealInteriorPanel();
        var assets = MakeAssets(returns.Length);
        var estimator = new SampleCovarianceEstimator();

        var sparse = new SparseMinimumVarianceConstruction(estimator, delta: decimal.MaxValue);
        var standard = new MinimumVarianceConstruction(estimator);

        var sparseW = sparse.ComputeTargetWeights(assets, returns);
        var standardW = standard.ComputeTargetWeights(assets, returns);

        foreach (var asset in assets)
        {
            AssertWithinTolerance(sparseW[asset], standardW[asset], PrecisionNumeric,
                $"δ=∞ vs §13 weight[{asset.Ticker}]: ");
        }
    }

    [Fact]
    public void SparseMinimumVariance_DeltaOne_EqualsStandardLongOnlyMinVariance()
    {
        // δ=1 is the long-only corner: ‖w‖₁=1 with Σw=1 ⟺ every w≥0, so the LASSO
        // optimum equals the long-only min-var (ActiveSetQpSolver lb=0). Asserted as a
        // solver-to-solver equivalence with MinimumVarianceConstruction on Panel B
        // (whose unconstrained min-var goes short, so this is a non-trivial corner).
        var cov = new decimal[,]
        {
            { 0.0100m, 0.01045m, 0.0018m },
            { 0.01045m, 0.0121m, 0.00198m },
            { 0.0018m, 0.00198m, 0.0324m },
        };
        var assets = MakeAssets(3);
        var estimator = new FixedCovarianceEstimator(cov);

        var sparse = new SparseMinimumVarianceConstruction(estimator, delta: 1.0m);
        var standard = new MinimumVarianceConstruction(estimator);

        var sparseW = sparse.ComputeTargetWeights(assets, PlaceholderReturns(3));
        var standardW = standard.ComputeTargetWeights(assets, PlaceholderReturns(3));

        foreach (var asset in assets)
        {
            AssertWithinTolerance(sparseW[asset], standardW[asset], PrecisionNumeric,
                $"δ=1 long-only weight[{asset.Ticker}]: ");
            // long-only corner: no shorts.
            Assert.True(sparseW[asset] >= -PrecisionNumeric, $"δ=1 weight {asset.Ticker} negative");
        }
    }

    [Fact]
    public void SparseMinimumVariance_BindingDelta_DiffersFromInfiniteDelta()
    {
        // Proves the ℓ₁ budget actually binds (not a min-var passthrough): on Panel B
        // the δ=1.3 budget produces a genuinely different solution than δ=∞ — strictly
        // lower gross exposure (the budget caps it) and a different weight vector.
        var cov = new decimal[,]
        {
            { 0.0100m, 0.01045m, 0.0018m },
            { 0.01045m, 0.0121m, 0.00198m },
            { 0.0018m, 0.00198m, 0.0324m },
        };
        var assets = MakeAssets(3);
        var estimator = new FixedCovarianceEstimator(cov);

        var binding = new SparseMinimumVarianceConstruction(estimator, delta: 1.3m);
        var slack = new SparseMinimumVarianceConstruction(estimator, delta: decimal.MaxValue);

        var bindingW = binding.ComputeTargetWeights(assets, PlaceholderReturns(3));
        var slackW = slack.ComputeTargetWeights(assets, PlaceholderReturns(3));

        var bindingGross = bindingW.Values.Select(Math.Abs).Sum();
        var slackGross = slackW.Values.Select(Math.Abs).Sum();

        // The budget binds: capped gross strictly below the unconstrained gross.
        bindingGross.Should().BeLessThan(slackGross - 0.01m);
        AssertWithinTolerance(bindingGross, 1.3m, 1e-4m, "binding gross at budget: ");

        // Weight vectors are genuinely different.
        var maxDiff = assets.Max(a => Math.Abs(bindingW[a] - slackW[a]));
        maxDiff.Should().BeGreaterThan(0.05m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-4: degenerate-input guards
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SparseMinimumVariance_SingleAsset_ReturnsFullWeight()
    {
        var returns = new[] { new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m } };
        var model = new SparseMinimumVarianceConstruction();
        var assets = MakeAssets(1);

        var result = model.ComputeTargetWeights(assets, returns);

        result.Should().HaveCount(1);
        AssertWithinTolerance(result[assets[0]], 1m, PrecisionNumeric, "Sparse single asset: ");
    }

    [Fact]
    public void SparseMinimumVariance_DeltaBelowOne_Throws()
    {
        // δ < 1 is infeasible for a long-only sum-to-one portfolio (gross ≥ 1 always).
        var act = () => new SparseMinimumVarianceConstruction(delta: 0.9m);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void SparseMinimumVariance_NullReturns_Throws()
    {
        var model = new SparseMinimumVarianceConstruction();
        var act = () => model.ComputeTargetWeights(MakeAssets(2), null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SparseMinimumVariance_MismatchedReturnsLength_Throws()
    {
        var model = new SparseMinimumVarianceConstruction();
        var act = () => model.ComputeTargetWeights(MakeAssets(3), new decimal[2][]);
        act.Should().Throw<ArgumentException>();
    }
}
