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
/// <see cref="EntropyPoolingConstruction"/> (Meucci Fully-Flexible-Views / Entropy Pooling).
///
/// Validates the C# implementation against own-formula Python reference vectors
/// (tests/Verification/vectors/entropy_pooling_*.json, from
/// generate_entropy_pooling_vectors.py — NOT pypfopt/cvxpy or any external EP library).
///
/// Reference: Meucci, A. (2010), "Fully Flexible Views: Theory and Practice",
/// arXiv 1012.2848 (SSRN 1213325).
/// </summary>
public sealed class EntropyPoolingCrossLanguageTests : CrossLanguageVerificationBase
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

    private static IReadOnlyList<Symbol> MakeAssets(int n)
    {
        return Enumerable.Range(0, n)
            .Select(i => new Symbol($"ASSET{i}"))
            .ToList();
    }

    private static void AssertWeightsSumToOne(IReadOnlyDictionary<Symbol, decimal> weights, string label)
    {
        AssertWithinTolerance(weights.Values.Sum(), 1m, PrecisionNumeric, $"{label} weights sum: ");
    }

    private static void AssertWeightsNonNegative(IReadOnlyDictionary<Symbol, decimal> weights, string label)
    {
        foreach (var (asset, w) in weights)
        {
            Assert.True(w >= -PrecisionNumeric, $"{label} weight for {asset.Ticker} = {w} is negative");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-2: Cross-language vectors — scenario Entropy Pooling, 1 equality view
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EntropyPooling_Scenario3Asset_PosteriorMeanMatchesPythonOwnFormula()
    {
        // The EP-specific own-formula ground truth is the confidence-blended POSTERIOR MEAN
        // (the dual reweighting is what EntropyPooling computes). We verify it against the
        // Python reference at PrecisionNumeric; the downstream MeanVarianceConstruction step is
        // already cross-language-verified independently, so the weight assertion below only
        // pins the interface invariants (non-negative, sum-to-1), not optimizer-dependent values.
        using var doc = LoadVector("entropy_pooling_scenario_3asset");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var pick = GetMatrix(inputs.GetProperty("pick_matrix"));
        var viewReturns = GetDecimalArray(inputs.GetProperty("view_returns"));
        var expectedMean = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("posterior_mean"));

        var assets = MakeAssets(returns.Length);
        var model = new EntropyPoolingConstruction(
            pickMatrix: pick,
            viewReturns: viewReturns,
            confidence: 1.0m,
            riskAversion: 1.0m);

        var posteriorMean = model.ComputePosteriorMean(returns);
        for (var i = 0; i < expectedMean.Length; i++)
        {
            AssertWithinTolerance(posteriorMean[i], expectedMean[i], PrecisionNumeric, $"EP_Scenario3 μ̃[{i}]: ");
        }

        var result = model.ComputeTargetWeights(assets, returns);
        AssertWeightsSumToOne(result, "EP_Scenario3");
        AssertWeightsNonNegative(result, "EP_Scenario3");
    }

    [Fact]
    public void EntropyPooling_Scenario3Asset_PosteriorSatisfiesView()
    {
        // The defining property of Entropy Pooling: the posterior expectation satisfies
        // the equality view. We confirm the C# posterior mean equals the reference's
        // realized view value (which the Python cross-check asserts == the view target).
        using var doc = LoadVector("entropy_pooling_scenario_3asset");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var pick = GetMatrix(inputs.GetProperty("pick_matrix"));
        var viewReturns = GetDecimalArray(inputs.GetProperty("view_returns"));
        var expectedMean = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("posterior_mean"));
        var viewRealized = GetDecimal(doc.RootElement.GetProperty("expected"), "view_realized");

        var assets = MakeAssets(returns.Length);
        var model = new EntropyPoolingConstruction(
            pickMatrix: pick, viewReturns: viewReturns, confidence: 1.0m);

        var posteriorMean = model.ComputePosteriorMean(returns);

        for (var i = 0; i < expectedMean.Length; i++)
        {
            AssertWithinTolerance(posteriorMean[i], expectedMean[i], PrecisionNumeric, $"PosteriorMean[{i}]: ");
        }

        // A·μ̃ = viewReturns ⇒ for the single asset-0 view, μ̃_0 == view target.
        AssertWithinTolerance(posteriorMean[0], viewReturns[0], PrecisionNumeric, "View satisfied: ");
        AssertWithinTolerance(posteriorMean[0], viewRealized, PrecisionNumeric, "View matches Python: ");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-3: FIDELITY — Gaussian-limit recovers Black-Litterman (the defining property)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EntropyPooling_GaussianLimit_PosteriorMeanMatchesBlackLittermanClosedForm()
    {
        // Meucci (2010): a Gaussian prior + full-confidence (c=1) linear expectation views
        // recovers μ̃ = μ + ΣQᵀ(QΣQᵀ)⁻¹(q − Qμ). We compare the EntropyPooling Gaussian-mode
        // posterior mean against the Python BL closed-form reference on a real 4-asset panel.
        using var doc = LoadVector("entropy_pooling_gaussian_bl");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var pick = GetMatrix(inputs.GetProperty("pick_matrix"));
        var viewReturns = GetDecimalArray(inputs.GetProperty("view_returns"));
        var equilibrium = GetDecimalArray(inputs.GetProperty("equilibrium_weights"));
        var expectedBlMean = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("bl_posterior_mean"));

        var model = EntropyPoolingConstruction.FromGaussianPrior(
            equilibriumWeights: equilibrium,
            pickMatrix: pick,
            viewReturns: viewReturns);

        var posteriorMean = model.ComputePosteriorMean(returns);

        Assert.Equal(expectedBlMean.Length, posteriorMean.Length);
        for (var i = 0; i < expectedBlMean.Length; i++)
        {
            AssertWithinTolerance(posteriorMean[i], expectedBlMean[i], PrecisionNumeric,
                $"BL Gaussian-limit posterior mean[{i}]: ");
        }
    }

    [Fact]
    public void EntropyPooling_GaussianLimit_WeightsMatchBlackLittermanPosteriorThroughSameOptimizer()
    {
        // End-to-end fidelity of the WIRED weight pipeline: EntropyPooling (Gaussian mode)
        // produces the same target weights as feeding the Black-Litterman posterior mean
        // (the Python BL closed-form reference) through the SAME mean-variance optimizer that
        // EntropyPooling uses internally. This proves (a) EP's Gaussian posterior mean IS the
        // BL mean and (b) the weight pipeline is wired and faithful — without conflating BL's
        // analytic w = Σ⁻¹μ/δ with EP's projected-gradient MV (two different optimizers).
        //
        // The reference BL posterior mean is the independent ground truth; we drive it through
        // MeanVarianceConstruction with a fixed-mean covariance estimator so the only moving
        // part is the posterior mean — the exact quantity the BL-special-case property is about.
        using var doc = LoadVector("entropy_pooling_gaussian_bl");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var pick = GetMatrix(inputs.GetProperty("pick_matrix"));
        var viewReturns = GetDecimalArray(inputs.GetProperty("view_returns"));
        var equilibrium = GetDecimalArray(inputs.GetProperty("equilibrium_weights"));
        var blMean = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("bl_posterior_mean"));

        var n = returns.Length;
        var assets = MakeAssets(n);

        var epModel = EntropyPoolingConstruction.FromGaussianPrior(
            equilibriumWeights: equilibrium,
            pickMatrix: pick,
            viewReturns: viewReturns);

        var epWeights = epModel.ComputeTargetWeights(assets, returns);

        // Reference weights: feed the BL posterior mean through MeanVarianceConstruction.
        // MeanVarianceConstruction recomputes the mean from the supplied series, so we craft a
        // synthetic two-row series whose sample mean equals the BL posterior mean and whose
        // sample covariance equals the panel covariance (a ±delta construction around the mean).
        var refWeights = MeanVarianceWeightsForMean(blMean, returns, assets);

        Assert.Equal(refWeights.Count, epWeights.Count);
        foreach (var asset in assets)
        {
            AssertWithinTolerance(epWeights[asset], refWeights[asset], PrecisionNumeric,
                $"EP vs BL-posterior weight {asset.Ticker}: ");
        }
    }

    /// <summary>
    /// Computes mean-variance weights for a target mean vector, using the panel covariance,
    /// by injecting a fixed covariance estimator and a synthetic series whose per-asset mean
    /// equals <paramref name="targetMean"/>. Mirrors EntropyPooling's internal MV step
    /// (riskAversion = 1.0, the Gaussian-mode default).
    /// </summary>
    private static IReadOnlyDictionary<Symbol, decimal> MeanVarianceWeightsForMean(
        decimal[] targetMean, decimal[][] returns, IReadOnlyList<Symbol> assets)
    {
        var n = returns.Length;
        var cov = new SampleCovarianceEstimator().Estimate(returns);

        // Synthetic 2-observation series: {mean + d, mean - d}. Its average == mean; the chosen
        // estimator ignores it and returns the fixed panel covariance, so only the mean drives MV.
        var synthetic = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            synthetic[i] = [targetMean[i] + 0.01m, targetMean[i] - 0.01m];
        }

        var mv = new MeanVarianceConstruction(
            covarianceEstimator: new FixedCovarianceEstimator(cov),
            riskAversion: 1.0m);

        return mv.ComputeTargetWeights(assets, synthetic);
    }

    /// <summary>A covariance estimator that always returns a fixed matrix (test helper).</summary>
    private sealed class FixedCovarianceEstimator(decimal[,] fixedCov) : ICovarianceEstimator
    {
        public decimal[,] Estimate(decimal[][] returns) => (decimal[,])fixedCov.Clone();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-1 / AC-4: no-views path + degenerate-input guards
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EntropyPooling_NoViews_ReturnsPriorImpliedMomentsAndValidWeights()
    {
        // No views ⇒ EP posterior == prior (1/J each) ⇒ posterior mean == the raw sample mean.
        // We verify the posterior mean against the Python prior mean (the EP-specific own-formula
        // ground truth) and confirm the downstream weights are valid (sum-to-1, non-negative).
        using var doc = LoadVector("entropy_pooling_no_views");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var expectedMean = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("prior_mean"));

        var assets = MakeAssets(returns.Length);
        var model = new EntropyPoolingConstruction(); // no views

        var posteriorMean = model.ComputePosteriorMean(returns);
        for (var i = 0; i < expectedMean.Length; i++)
        {
            AssertWithinTolerance(posteriorMean[i], expectedMean[i], PrecisionNumeric, $"EP_NoViews μ̃[{i}]: ");
        }

        var result = model.ComputeTargetWeights(assets, returns);
        AssertWeightsSumToOne(result, "EP_NoViews");
        AssertWeightsNonNegative(result, "EP_NoViews");
    }

    [Fact]
    public void EntropyPooling_EmptyAssets_ReturnsEmpty()
    {
        var model = new EntropyPoolingConstruction();
        var result = model.ComputeTargetWeights([], []);
        result.Should().BeEmpty();
    }

    [Fact]
    public void EntropyPooling_NullReturns_Throws()
    {
        var model = new EntropyPoolingConstruction();
        var act = () => model.ComputeTargetWeights(MakeAssets(2), null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntropyPooling_MismatchedReturnsLength_Throws()
    {
        var model = new EntropyPoolingConstruction();
        var act = () => model.ComputeTargetWeights(MakeAssets(3), new decimal[2][]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntropyPooling_SingleAsset_ReturnsFullWeight()
    {
        // Single asset: weight must be 1.0 (degenerate but valid).
        var returns = new[] { new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m } };
        var model = new EntropyPoolingConstruction();
        var assets = MakeAssets(1);
        var result = model.ComputeTargetWeights(assets, returns);

        result.Should().HaveCount(1);
        AssertWithinTolerance(result[assets[0]], 1m, PrecisionNumeric, "SingleAsset: ");
    }

    [Fact]
    public void EntropyPooling_InconsistentView_ThrowsCalculationException()
    {
        // A view target outside the convex hull of the scenario view-scores is infeasible:
        // no probability reweighting can hit it. The dual diverges → surface, don't silently
        // propagate a wrong posterior.
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m },
            new[] { 0.02m, 0.01m, -0.01m, 0.00m, 0.01m },
        };
        // Asset 0 returns range [-0.02, 0.03]; demand expectation = 0.5 (far outside the hull).
        var pick = new decimal[,] { { 1m, 0m } };
        var view = new[] { 0.5m };

        var model = new EntropyPoolingConstruction(pickMatrix: pick, viewReturns: view);
        var act = () => model.ComputeTargetWeights(MakeAssets(2), returns);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void EntropyPooling_PickMatrixWithoutViewReturns_Throws()
    {
        var pick = new decimal[,] { { 1m, 0m } };
        var act = () => new EntropyPoolingConstruction(pickMatrix: pick, viewReturns: null);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void EntropyPooling_ConfidenceOutOfRange_Throws(double c)
    {
        var act = () => new EntropyPoolingConstruction(confidence: (decimal)c);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BLPRIOR-001: equilibrium weights are the BL prior anchor and MUST drive
    // the posterior. The BL prior mean is the reverse-optimized equilibrium
    // return Π = δ·Σ·w_eq, so two models differing ONLY in equilibrium weights
    // must produce different posterior means on the unviewed components.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EntropyPooling_GaussianPrior_EquilibriumWeightsDriveThePosterior()
    {
        // A single full-confidence view on asset 0 pins μ̃_0 to the view target, but the
        // BL prior mean Π = δ·Σ·w_eq drives the unviewed assets (1, 2, 3). Two equilibrium
        // vectors that differ radically must therefore produce different posterior means.
        // Before BLPRIOR-001, the prior was the SAMPLE mean and equilibrium weights were
        // dead, so both posteriors were bit-identical and this assertion fails.
        using var doc = LoadVector("entropy_pooling_gaussian_bl");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var pick = GetMatrix(inputs.GetProperty("pick_matrix"));
        var viewReturns = GetDecimalArray(inputs.GetProperty("view_returns"));

        var eqA = new[] { 0.25m, 0.25m, 0.25m, 0.25m };
        var eqB = new[] { 0.70m, 0.10m, 0.10m, 0.10m };

        var modelA = EntropyPoolingConstruction.FromGaussianPrior(
            equilibriumWeights: eqA, pickMatrix: pick, viewReturns: viewReturns);
        var modelB = EntropyPoolingConstruction.FromGaussianPrior(
            equilibriumWeights: eqB, pickMatrix: pick, viewReturns: viewReturns);

        var meanA = modelA.ComputePosteriorMean(returns);
        var meanB = modelB.ComputePosteriorMean(returns);

        var maxDiff = 0m;
        for (var i = 0; i < meanA.Length; i++)
        {
            maxDiff = Math.Max(maxDiff, Math.Abs(meanA[i] - meanB[i]));
        }

        Assert.True(maxDiff > 1e-6m,
            $"BLPRIOR-001: equilibrium weights must drive the BL prior Π=δΣw_eq; posterior means "
            + $"differed by only {maxDiff} between two radically different equilibrium vectors.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PICKDIM-002: the pick-matrix asset (column) dimension must be validated
    // against the runtime panel width, surfacing a mismatch as ArgumentException
    // (repo convention) rather than IndexOutOfRangeException or a silent result.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EntropyPooling_GaussianPrior_PickMatrixWiderThanPanel_ThrowsArgumentException()
    {
        // Pick matrix built for N0=3 columns, invoked on a 4-asset panel (n=4 > N0=3).
        // Before the guard this threw a raw IndexOutOfRangeException at the q[a,i] read.
        var pick = new decimal[,] { { 1m, 0m, 0m } };
        var view = new[] { 0.0015m };
        var eq = new[] { 0.25m, 0.25m, 0.25m };
        var model = EntropyPoolingConstruction.FromGaussianPrior(
            equilibriumWeights: eq, pickMatrix: pick, viewReturns: view);

        var assets = MakeAssets(4);
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m },
            new[] { 0.02m, 0.01m, -0.01m, 0.00m, 0.01m },
            new[] { -0.01m, 0.02m, 0.00m, 0.01m, -0.02m },
            new[] { 0.00m, 0.01m, 0.02m, -0.01m, 0.01m },
        };

        var act = () => model.ComputeTargetWeights(assets, returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntropyPooling_GaussianPrior_PickMatrixNarrowerThanPanel_ThrowsArgumentException()
    {
        // Pick matrix built for N0=3 columns, invoked on a 2-asset panel (n=2 < N0=3).
        // Before the guard this produced a SILENT, numerically-wrong posterior with no error.
        var pick = new decimal[,] { { 1m, 0m, 0m } };
        var view = new[] { 0.0015m };
        var eq = new[] { 0.25m, 0.25m, 0.25m };
        var model = EntropyPoolingConstruction.FromGaussianPrior(
            equilibriumWeights: eq, pickMatrix: pick, viewReturns: view);

        var assets = MakeAssets(2);
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m },
            new[] { 0.02m, 0.01m, -0.01m, 0.00m, 0.01m },
        };

        var act = () => model.ComputeTargetWeights(assets, returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntropyPooling_Scenario_PickMatrixDimensionMismatch_ThrowsArgumentException()
    {
        // Scenario (non-Gaussian) path: a pick matrix for N0=3 invoked on a 2-asset panel
        // reaches SolveEntropyPoolingDual's _pickMatrix[a,i] read; the guard must surface it.
        var pick = new decimal[,] { { 1m, 0m, 0m } };
        var view = new[] { 0.01m };
        var model = new EntropyPoolingConstruction(pickMatrix: pick, viewReturns: view);

        var assets = MakeAssets(2);
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m },
            new[] { 0.02m, 0.01m, -0.01m, 0.00m, 0.01m },
        };

        var act = () => model.ComputeTargetWeights(assets, returns);
        act.Should().Throw<ArgumentException>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EQWTSDIM-008: equilibriumWeights.Length must equal the pick-matrix
    // column count (N) at factory time. A mismatch must throw ArgumentException
    // from FromGaussianPrior — not leak IndexOutOfRangeException at compute
    // time (short weights) or silently produce a wrong posterior (long weights).
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EntropyPooling_GaussianPrior_EquilibriumWeightsTooShort_ThrowsArgumentException()
    {
        // Pick matrix has N=3 columns; equilibriumWeights has length 2 < N.
        // Before EQWTSDIM-008 guard: FromGaussianPrior accepted this and
        // IndexOutOfRangeException occurred later at ComputeGaussianPosterior:415.
        var pick = new decimal[,] { { 1m, 0m, 0m } };
        var view = new[] { 0.0015m };
        var eqShort = new[] { 0.5m, 0.5m }; // length 2, N=3

        var act = () => EntropyPoolingConstruction.FromGaussianPrior(
            equilibriumWeights: eqShort, pickMatrix: pick, viewReturns: view);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*equilibriumWeights*");
    }

    [Fact]
    public void EntropyPooling_GaussianPrior_EquilibriumWeightsTooLong_ThrowsArgumentException()
    {
        // Pick matrix has N=3 columns; equilibriumWeights has length 4 > N.
        // Before EQWTSDIM-008 guard: FromGaussianPrior accepted this and silently
        // produced a wrong posterior (sub-vector consumed).
        var pick = new decimal[,] { { 1m, 0m, 0m } };
        var view = new[] { 0.0015m };
        var eqLong = new[] { 0.25m, 0.25m, 0.25m, 0.25m }; // length 4, N=3

        var act = () => EntropyPoolingConstruction.FromGaussianPrior(
            equilibriumWeights: eqLong, pickMatrix: pick, viewReturns: view);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*equilibriumWeights*");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NULLGAP-011: ComputePosteriorMean has a null/empty guard (lines 263-265)
    // that is untested. Pin it so future regression (accidental guard deletion)
    // is caught.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EntropyPooling_ComputePosteriorMean_NullReturns_ThrowsArgumentException()
    {
        var model = new EntropyPoolingConstruction();
        var act = () => model.ComputePosteriorMean(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntropyPooling_ComputePosteriorMean_EmptyReturns_ThrowsArgumentException()
    {
        var model = new EntropyPoolingConstruction();
        var act = () => model.ComputePosteriorMean(Array.Empty<decimal[]>());
        act.Should().Throw<ArgumentException>();
    }
}
