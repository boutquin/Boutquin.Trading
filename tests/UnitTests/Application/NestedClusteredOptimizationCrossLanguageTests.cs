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

using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification and fidelity tests for
/// <see cref="NestedClusteredOptimizationConstruction"/> (López de Prado NCO).
///
/// Validates the C# implementation against own-formula Python reference vectors
/// (tests/Verification/vectors/nco_*.json, from generate_nco_vectors.py — NOT
/// pypfopt/cvxpy/scikit-learn or any external NCO/k-means library). The deterministic
/// farthest-point K-Means seeding and mean-silhouette K selection are replicated
/// identically in both languages so the vectors are reproducible cross-language.
///
/// Reference: López de Prado, M. (2019), "A Robust Estimator of the Efficient
/// Frontier", SSRN 3469961 (Snippet 4).
/// </summary>
public sealed class NestedClusteredOptimizationCrossLanguageTests : CrossLanguageVerificationBase
{
    // ─── Helpers ────────────────────────────────────────────────────────

    private static decimal[][] GetJaggedArray(JsonElement element)
    {
        var outer = element.EnumerateArray().ToArray();
        var result = new decimal[outer.Length][];
        for (var i = 0; i < outer.Length; i++)
        {
            result[i] = outer[i].EnumerateArray()
                .Select(e => (decimal)e.GetDouble())
                .ToArray();
        }

        return result;
    }

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

    /// <summary>A covariance estimator that always returns a fixed matrix (test helper).</summary>
    private sealed class FixedCovarianceEstimator(decimal[,] fixedCov) : ICovarianceEstimator
    {
        public decimal[,] Estimate(decimal[][] returns) => (decimal[,])fixedCov.Clone();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-1: implements IPortfolioConstructionModel (weights ≥ 0, sum 1.0, empty→empty)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Nco_IsPortfolioConstructionModel()
    {
        var model = new NestedClusteredOptimizationConstruction();
        model.Should().BeAssignableTo<IPortfolioConstructionModel>();
    }

    [Fact]
    public void Nco_EmptyAssets_ReturnsEmpty()
    {
        var model = new NestedClusteredOptimizationConstruction();
        var result = model.ComputeTargetWeights([], []);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Nco_RealPanel_WeightsSumToOneAndNonNegative()
    {
        using var doc = LoadVector("nco_numeric_6asset");
        var returns = GetJaggedArray(doc.RootElement.GetProperty("inputs").GetProperty("returns"));
        var assets = MakeAssets(returns.Length);

        var model = new NestedClusteredOptimizationConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        // AC-1 interface invariants: every weight ≥ 0 and the weights sum to 1.0.
        AssertWithinTolerance(result.Values.Sum(), 1m, PrecisionNumeric, "NCO weights sum: ");
        foreach (var (asset, w) in result)
        {
            Assert.True(w >= -PrecisionNumeric, $"NCO weight {asset.Ticker} = {w} is negative");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-2: cross-language vectors — full 4-step NCO on a real 6-asset 2-block panel
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Nco_RealPanel_WeightsMatchPythonOwnFormula()
    {
        // The full wired pipeline (clustering → intra min-var → collapse → inter min-var →
        // recombine) must reproduce the Python own-formula reference to PrecisionNumeric.
        // This pins the deterministic K-Means + silhouette-K selection cross-language.
        using var doc = LoadVector("nco_numeric_6asset");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var expected = doc.RootElement.GetProperty("expected");
        var expectedWeights = GetDecimalArray(expected.GetProperty("weights"));

        var assets = MakeAssets(returns.Length);
        var model = new NestedClusteredOptimizationConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        for (var i = 0; i < expectedWeights.Length; i++)
        {
            AssertWithinTolerance(result[assets[i]], expectedWeights[i], PrecisionNumeric,
                $"NCO weight[{i}]: ");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-3: FIDELITY — block-diagonal exactness (the defining accuracy contract)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Nco_BlockDiagonalCovariance_WeightsEqualAnalyticNestedMinVar()
    {
        // López de Prado (2019) accuracy contract: when the covariance is block-diagonal
        // (clusters uncorrelated) the inter-cluster optimizer sees a near-identity reduced
        // covariance, so NCO equals the analytically-correct nested min-variance solution:
        // each asset's intra inverse-variance weight × its cluster's inter inverse-variance
        // share. We inject the exact block-diagonal covariance and assert equality with the
        // independently-derived analytic target from the Python reference.
        using var doc = LoadVector("nco_block_diagonal_exact");
        var cov = GetMatrix(doc.RootElement.GetProperty("inputs").GetProperty("covariance"));
        var analytic = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("analytic_weights"));

        var n = cov.GetLength(0);
        var assets = MakeAssets(n);

        // Inject the exact block-diagonal covariance; returns are a placeholder the fixed
        // estimator ignores (the cross-language ground truth is the covariance, not a panel).
        var placeholder = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            placeholder[i] = new[] { 0.01m, -0.01m };
        }

        var model = new NestedClusteredOptimizationConstruction(new FixedCovarianceEstimator(cov));
        var result = model.ComputeTargetWeights(assets, placeholder);

        for (var i = 0; i < analytic.Length; i++)
        {
            AssertWithinTolerance(result[assets[i]], analytic[i], PrecisionNumeric,
                $"NCO block-diagonal exact weight[{i}]: ");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-4: degenerate-input guards
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Nco_SingleAsset_ReturnsFullWeight()
    {
        var returns = new[] { new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m } };
        var model = new NestedClusteredOptimizationConstruction();
        var assets = MakeAssets(1);

        var result = model.ComputeTargetWeights(assets, returns);

        result.Should().HaveCount(1);
        AssertWithinTolerance(result[assets[0]], 1m, PrecisionNumeric, "NCO single asset: ");
    }

    [Fact]
    public void Nco_SingleCluster_ReducesToPlainMinVariance()
    {
        // When all assets are highly correlated the silhouette selection collapses to a
        // single cluster and NCO must reduce to plain minimum-variance on the whole
        // covariance. We inject a diagonal covariance (whose min-var is analytic
        // inverse-variance) and FORCE the single-cluster regime via maxClusters: 1.
        using var doc = LoadVector("nco_single_cluster_minvar");
        var cov = GetMatrix(doc.RootElement.GetProperty("inputs").GetProperty("covariance"));
        var minvar = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("minvar_weights"));

        var n = cov.GetLength(0);
        var assets = MakeAssets(n);
        var placeholder = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            placeholder[i] = new[] { 0.01m, -0.01m };
        }

        // maxClusters: 1 → no candidate K ≥ 2 is scanned → single cluster → plain min-var.
        var model = new NestedClusteredOptimizationConstruction(
            new FixedCovarianceEstimator(cov), maxClusters: 1);
        var result = model.ComputeTargetWeights(assets, placeholder);

        for (var i = 0; i < minvar.Length; i++)
        {
            AssertWithinTolerance(result[assets[i]], minvar[i], PrecisionNumeric,
                $"NCO single-cluster min-var weight[{i}]: ");
        }
    }

    [Fact]
    public void Nco_NullReturns_Throws()
    {
        var model = new NestedClusteredOptimizationConstruction();
        var act = () => model.ComputeTargetWeights(MakeAssets(2), null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Nco_MismatchedReturnsLength_Throws()
    {
        var model = new NestedClusteredOptimizationConstruction();
        var act = () => model.ComputeTargetWeights(MakeAssets(3), new decimal[2][]);
        act.Should().Throw<ArgumentException>();
    }
}
