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

namespace Boutquin.Trading.Application.PortfolioConstruction;

using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Domain.Exceptions;

/// <summary>
/// Implements Entropy Pooling (Fully Flexible Views) portfolio construction — Meucci's
/// distribution-free generalization of the Black-Litterman model.
/// </summary>
/// <remarks>
/// <para>
/// Reference: Meucci, A. (2010). "Fully Flexible Views: Theory and Practice."
/// <i>Risk</i>, 21(10), 97-102 (arXiv:1012.2848, SSRN 1213325).
/// </para>
/// <para>
/// The prior is the empirical panel: scenario <c>j</c> is the cross-section of asset
/// returns at time <c>j</c>, with prior probability <c>p_j = 1/T</c>. Views are linear
/// equality constraints on posterior <i>expectations</i>: <c>A·μ̃ = b</c>, where <c>A</c>
/// is a <c>K×N</c> pick matrix and <c>b</c> the <c>K</c> target view expectations.
/// </para>
/// <para>
/// The Entropy Pooling posterior probabilities minimize the Kullback-Leibler divergence
/// to the prior subject to the view constraints. The dual (exponential-family) solution is
/// <c>p̃_j ∝ p_j · exp(λᵀ·(A·X_j))</c>, where the <c>K</c>-dimensional <c>λ</c> solves a
/// convex dual (O(K²) per Newton step, <b>not</b> O(J)). The dual
/// <c>D(λ) = log(Σ_j p_j exp(λᵀ V_j)) − λᵀb</c> is convex with gradient
/// <c>E_p̃[V] − b</c> and Hessian <c>Cov_p̃[V]</c> (PSD ⇒ global convergence); the
/// exponential form keeps <c>p̃ > 0</c> automatically.
/// </para>
/// <para>
/// The confidence blend is <c>pᶜ = (1−c)·p + c·p̃</c>. Posterior moments <c>μ̃</c> and
/// <c>Σ̃</c> are computed under <c>pᶜ</c> and fed to mean-variance optimization
/// (<see cref="MeanVarianceConstruction"/> machinery).
/// </para>
/// <para>
/// <b>Black-Litterman is the special case:</b> a Gaussian prior <c>(μ, Σ)</c> with
/// full-confidence (<c>c = 1</c>) linear expectation views recovers the closed form
/// <c>μ̃ = μ + Σ Qᵀ (Q Σ Qᵀ)⁻¹ (q − Q μ)</c>. The
/// <see cref="FromGaussianPrior"/> factory drives this path so the posterior reduces
/// exactly to <see cref="BlackLittermanConstruction"/> in the Gaussian limit.
/// </para>
/// <para>
/// V1 supports equality views on expectations plus a single confidence scalar. Intended
/// omissions (not implemented): multi-user opinion pooling, copula/tail-codependence
/// views, and higher cross-moment views.
/// </para>
/// </remarks>
public sealed class EntropyPoolingConstruction : IPortfolioConstructionModel
{
    private readonly decimal[,]? _pickMatrix;     // A: K x N
    private readonly decimal[]? _viewReturns;     // b: K
    private readonly decimal _confidence;         // c in [0, 1]
    private readonly decimal _riskAversion;       // lambda for the MV optimizer
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;

    // Gaussian-limit (Black-Litterman) inputs — non-null only via FromGaussianPrior.
    private readonly bool _gaussianMode;
    private readonly decimal[]? _equilibriumWeights;
    private readonly decimal _blRiskAversion;
    private readonly ICovarianceEstimator _covarianceEstimator;

    /// <summary>
    /// Initializes a scenario (empirical) Entropy Pooling construction model.
    /// </summary>
    /// <param name="pickMatrix">
    /// The view pick matrix <c>A</c> (<c>K×N</c>): <c>A·μ̃ = b</c> constrains posterior
    /// expectations. Null (with null <paramref name="viewReturns"/>) means no views — the
    /// posterior equals the prior, and the optimizer runs on the raw sample moments.
    /// </param>
    /// <param name="viewReturns">The view target expectations <c>b</c> (<c>K</c>). Null if no views.</param>
    /// <param name="confidence">Confidence blend scalar <c>c</c> in <c>[0, 1]</c>. Default 1.0 (full confidence).</param>
    /// <param name="riskAversion">Risk-aversion parameter for the mean-variance step. Default 1.0.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0.</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0.</param>
    public EntropyPoolingConstruction(
        decimal[,]? pickMatrix = null,
        decimal[]? viewReturns = null,
        decimal confidence = 1.0m,
        decimal riskAversion = 1.0m,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m)
    {
        if (confidence is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence), confidence, "Confidence must be in [0, 1].");
        }

        if ((pickMatrix is null) != (viewReturns is null))
        {
            throw new ArgumentException(
                "pickMatrix and viewReturns must both be provided or both be null.");
        }

        if (pickMatrix is not null && viewReturns is not null
            && pickMatrix.GetLength(0) != viewReturns.Length)
        {
            throw new ArgumentException(
                $"pickMatrix row count ({pickMatrix.GetLength(0)}) must equal viewReturns length ({viewReturns.Length}).");
        }

        _pickMatrix = pickMatrix is not null ? (decimal[,])pickMatrix.Clone() : null;
        _viewReturns = viewReturns is not null ? (decimal[])viewReturns.Clone() : null;
        _confidence = confidence;
        _riskAversion = riskAversion;
        _minWeight = minWeight;
        _maxWeight = maxWeight;
        _gaussianMode = false;
        _covarianceEstimator = new SampleCovarianceEstimator();
    }

    private EntropyPoolingConstruction(
        decimal[] equilibriumWeights,
        decimal[,] pickMatrix,
        decimal[] viewReturns,
        decimal blRiskAversion,
        ICovarianceEstimator covarianceEstimator,
        decimal minWeight,
        decimal maxWeight)
    {
        _gaussianMode = true;
        _equilibriumWeights = (decimal[])equilibriumWeights.Clone();
        _pickMatrix = (decimal[,])pickMatrix.Clone();
        _viewReturns = (decimal[])viewReturns.Clone();
        _blRiskAversion = blRiskAversion;
        _covarianceEstimator = covarianceEstimator;
        _confidence = 1.0m;
        _riskAversion = 1.0m;
        _minWeight = minWeight;
        _maxWeight = maxWeight;
    }

    /// <summary>
    /// Creates an Entropy Pooling model in its Gaussian limit — the Black-Litterman special case.
    /// With a Gaussian prior (sample mean/covariance) and full-confidence (<c>c = 1</c>) linear
    /// expectation views, the posterior mean reduces to the Black-Litterman closed form
    /// <c>μ̃ = μ + Σ Qᵀ (Q Σ Qᵀ)⁻¹ (q − Q μ)</c>, fed to mean-variance optimization.
    /// </summary>
    /// <param name="equilibriumWeights">Market-cap / equilibrium weights (the BL prior anchor).</param>
    /// <param name="pickMatrix">The view pick matrix <c>Q</c> (<c>K×N</c>).</param>
    /// <param name="viewReturns">The full-confidence view returns <c>q</c> (<c>K</c>).</param>
    /// <param name="riskAversion">Risk-aversion coefficient (delta) used by the BL prior and the MV step. Default 2.5.</param>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0.</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0.</param>
    public static EntropyPoolingConstruction FromGaussianPrior(
        decimal[] equilibriumWeights,
        decimal[,] pickMatrix,
        decimal[] viewReturns,
        decimal riskAversion = 2.5m,
        ICovarianceEstimator? covarianceEstimator = null,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m)
    {
        Guard.AgainstNull(() => equilibriumWeights);
        Guard.AgainstNull(() => pickMatrix);
        Guard.AgainstNull(() => viewReturns);

        if (riskAversion <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(riskAversion), riskAversion, "Risk aversion must be strictly positive.");
        }

        if (pickMatrix.GetLength(0) != viewReturns.Length)
        {
            throw new ArgumentException(
                $"pickMatrix row count ({pickMatrix.GetLength(0)}) must equal viewReturns length ({viewReturns.Length}).");
        }

        if (equilibriumWeights.Length != pickMatrix.GetLength(1))
        {
            throw new ArgumentException(
                $"equilibriumWeights length ({equilibriumWeights.Length}) must equal the asset count "
                + $"derived from pickMatrix column count ({pickMatrix.GetLength(1)}).",
                nameof(equilibriumWeights));
        }

        return new EntropyPoolingConstruction(
            equilibriumWeights,
            pickMatrix,
            viewReturns,
            riskAversion,
            covarianceEstimator ?? new SampleCovarianceEstimator(),
            minWeight,
            maxWeight);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns)
    {
        Guard.AgainstNull(() => assets);

        if (assets.Count == 0)
        {
            return new Dictionary<Symbol, decimal>();
        }

        if (returns is null || returns.Length != assets.Count)
        {
            throw new ArgumentException("Returns array must have one series per asset.", nameof(returns));
        }

        ValidatePickMatrixWidth(assets.Count);

        var n = assets.Count;

        // Single asset: weight is trivially 1.0.
        if (n == 1)
        {
            return new Dictionary<Symbol, decimal> { [assets[0]] = 1m };
        }

        SampleCovarianceEstimator.ValidateReturns(returns);

        // 1. Posterior mean μ̃ and covariance Σ̃ under the confidence-blended posterior.
        var (mu, sigma) = ComputePosteriorMoments(returns);

        // 2. Feed (μ̃, Σ̃) to mean-variance optimization — reuse MeanVarianceConstruction
        //    machinery (the spec's required optimizer). A synthetic 2-observation series whose
        //    per-asset average equals μ̃, paired with a fixed-Σ̃ covariance estimator, drives the
        //    optimizer so the only signal is the EP posterior moments.
        var muDec = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            muDec[i] = (decimal)mu[i];
        }

        var sigmaDec = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                sigmaDec[i, j] = (decimal)sigma[i, j];
            }
        }

        var weights = MeanVarianceOnPosterior(assets, muDec, sigmaDec);

        return weights;
    }

    /// <summary>
    /// Computes the Entropy Pooling posterior mean vector <c>μ̃</c> (per asset) under the
    /// confidence-blended posterior probabilities. In Gaussian mode this is the
    /// Black-Litterman closed-form posterior mean.
    /// </summary>
    /// <param name="returns">The return panel: <c>returns[i]</c> is the series for asset <c>i</c>.</param>
    /// <returns>The posterior mean per asset.</returns>
    public decimal[] ComputePosteriorMean(decimal[][] returns)
    {
        if (returns is null || returns.Length == 0)
        {
            throw new ArgumentException("Returns array must not be null or empty.", nameof(returns));
        }

        ValidatePickMatrixWidth(returns.Length);

        SampleCovarianceEstimator.ValidateReturns(returns);

        var (mu, _) = ComputePosteriorMoments(returns);
        var n = mu.Length;
        var result = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = (decimal)mu[i];
        }

        return result;
    }

    /// <summary>
    /// Validates the pick-matrix asset (column) dimension against the runtime panel width.
    /// The pick matrix <c>A</c>/<c>Q</c> is <c>K×N</c> with <c>N</c> fixed at construction,
    /// but <c>N</c> is only matched against the panel at compute time. A mismatch is a paired-
    /// dimension fault that must surface as <see cref="ArgumentException"/> (repo convention)
    /// rather than leaking a raw <see cref="IndexOutOfRangeException"/> (n &gt; N) or silently
    /// producing a numerically wrong posterior off a sub-block of the matrix (n &lt; N).
    /// </summary>
    private void ValidatePickMatrixWidth(int assetCount)
    {
        if (_pickMatrix is not null && _pickMatrix.GetLength(1) != assetCount)
        {
            throw new ArgumentException(
                $"pickMatrix column count ({_pickMatrix.GetLength(1)}) must equal the asset count "
                + $"({assetCount}).");
        }
    }

    // ─── Posterior moments ──────────────────────────────────────────────────

    /// <summary>
    /// Computes (μ̃, Σ̃): the posterior mean and covariance under the confidence-blended
    /// Entropy Pooling probabilities (scenario mode) or the Black-Litterman closed form
    /// (Gaussian mode).
    /// </summary>
    private (double[] Mu, double[,] Sigma) ComputePosteriorMoments(decimal[][] returns)
    {
        var n = returns.Length;
        var t = returns[0].Length;

        if (_gaussianMode)
        {
            return ComputeGaussianPosterior(returns);
        }

        // Scenarios X (T x N): scenario j is the cross-section of returns at time j.
        var x = new double[t, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < t; j++)
            {
                x[j, i] = (double)returns[i][j];
            }
        }

        // Prior probabilities p_j = 1/T.
        var p = new double[t];
        for (var j = 0; j < t; j++)
        {
            p[j] = 1.0 / t;
        }

        // Entropy Pooling posterior probabilities p̃ from the dual; blend with prior.
        var pTilde = SolveEntropyPoolingDual(x, p, n, t);
        var c = (double)_confidence;
        var pc = new double[t];
        var pcSum = 0.0;
        for (var j = 0; j < t; j++)
        {
            pc[j] = (1.0 - c) * p[j] + c * pTilde[j];
            pcSum += pc[j];
        }

        for (var j = 0; j < t; j++)
        {
            pc[j] /= pcSum;
        }

        // Posterior moments under pc.
        var mu = new double[n];
        for (var i = 0; i < n; i++)
        {
            var sum = 0.0;
            for (var j = 0; j < t; j++)
            {
                sum += pc[j] * x[j, i];
            }

            mu[i] = sum;
        }

        var sigma = new double[n, n];
        for (var a = 0; a < n; a++)
        {
            for (var b = a; b < n; b++)
            {
                var sum = 0.0;
                for (var j = 0; j < t; j++)
                {
                    sum += pc[j] * (x[j, a] - mu[a]) * (x[j, b] - mu[b]);
                }

                sigma[a, b] = sum;
                sigma[b, a] = sum;
            }
        }

        return (mu, sigma);
    }

    /// <summary>
    /// Gaussian-limit posterior: the Black-Litterman closed form
    /// <c>μ̃ = Π + Σ Qᵀ (Q Σ Qᵀ)⁻¹ (q − Q Π)</c>, with Σ from the configured estimator and the
    /// prior mean <c>Π = δ·Σ·w_eq</c> — the reverse-optimized implied equilibrium return
    /// (δ = risk aversion, w_eq = the configured equilibrium weights). Anchoring on the
    /// implied-equilibrium return (rather than the sample mean) is the defining feature that
    /// distinguishes Black-Litterman from a naive Gaussian-prior-on-sample-mean. Σ̃ stays at
    /// the prior Σ (full-confidence views shift only the mean in the BL limit).
    /// </summary>
    private (double[] Mu, double[,] Sigma) ComputeGaussianPosterior(decimal[][] returns)
    {
        var n = returns.Length;

        var sigmaDec = _covarianceEstimator.Estimate(returns);
        var sigma = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                sigma[i, j] = (double)sigmaDec[i, j];
            }
        }

        // Black-Litterman prior mean = implied equilibrium return Π = δ·Σ·w_eq (reverse
        // optimization), NOT the sample mean. The equilibrium weights are the BL prior anchor.
        var delta = (double)_blRiskAversion;
        var mu = new double[n];
        for (var i = 0; i < n; i++)
        {
            var sum = 0.0;
            for (var l = 0; l < n; l++)
            {
                sum += sigma[i, l] * (double)_equilibriumWeights![l];
            }

            mu[i] = delta * sum;
        }

        var k = _viewReturns!.Length;
        var q = new double[k, n];
        for (var a = 0; a < k; a++)
        {
            for (var i = 0; i < n; i++)
            {
                q[a, i] = (double)_pickMatrix![a, i];
            }
        }

        // QΣ (K x N) and QΣQᵀ (K x K).
        var qSigma = new double[k, n];
        for (var a = 0; a < k; a++)
        {
            for (var i = 0; i < n; i++)
            {
                var sum = 0.0;
                for (var l = 0; l < n; l++)
                {
                    sum += q[a, l] * sigma[l, i];
                }

                qSigma[a, i] = sum;
            }
        }

        var qSigmaQt = new double[k, k];
        for (var a = 0; a < k; a++)
        {
            for (var b = 0; b < k; b++)
            {
                var sum = 0.0;
                for (var i = 0; i < n; i++)
                {
                    sum += qSigma[a, i] * q[b, i];
                }

                qSigmaQt[a, b] = sum;
            }
        }

        // (q − Qμ) (K).
        var qMinusQmu = new double[k];
        for (var a = 0; a < k; a++)
        {
            var qMu = 0.0;
            for (var i = 0; i < n; i++)
            {
                qMu += q[a, i] * mu[i];
            }

            qMinusQmu[a] = (double)_viewReturns[a] - qMu;
        }

        // (QΣQᵀ)⁻¹ (q − Qμ): solve the K-dim system.
        var inner = SolveLinearSystem(qSigmaQt, qMinusQmu, k);

        // μ̃ = μ + (QΣ)ᵀ inner.
        var muPost = new double[n];
        for (var i = 0; i < n; i++)
        {
            var add = 0.0;
            for (var a = 0; a < k; a++)
            {
                add += qSigma[a, i] * inner[a];
            }

            muPost[i] = mu[i] + add;
        }

        return (muPost, sigma);
    }

    // ─── Entropy Pooling dual (convex, Newton) ──────────────────────────────

    /// <summary>
    /// Solves the Entropy Pooling dual for equality views <c>A·μ̃ = b</c>, returning the
    /// posterior probabilities <c>p̃_j ∝ p_j exp(λᵀ V_j)</c> where <c>V_j = A·X_j</c> and
    /// the K-dimensional <c>λ</c> minimizes the convex dual
    /// <c>D(λ) = log(Σ_j p_j exp(λᵀ V_j)) − λᵀ b</c> (Newton with line search).
    /// </summary>
    private double[] SolveEntropyPoolingDual(double[,] x, double[] p, int n, int t)
    {
        // No views → posterior == prior.
        if (_pickMatrix is null || _viewReturns is null)
        {
            return (double[])p.Clone();
        }

        var k = _viewReturns.Length;

        // View-score matrix V (T x K): V[j,a] = A[a,:] · X_j.
        var v = new double[t, k];
        for (var j = 0; j < t; j++)
        {
            for (var a = 0; a < k; a++)
            {
                var sum = 0.0;
                for (var i = 0; i < n; i++)
                {
                    sum += (double)_pickMatrix[a, i] * x[j, i];
                }

                v[j, a] = sum;
            }
        }

        var b = new double[k];
        for (var a = 0; a < k; a++)
        {
            b[a] = (double)_viewReturns[a];
        }

        var lam = new double[k];
        const int maxIter = 200;
        const double tol = 1e-14;
        var lastGradMax = double.PositiveInfinity;

        for (var iter = 0; iter < maxIter; iter++)
        {
            var pTilde = PosteriorFromLambda(p, v, lam, t, k);

            // Gradient of the convex dual: E_p̃[V] − b.
            var ev = new double[k];
            for (var a = 0; a < k; a++)
            {
                var sum = 0.0;
                for (var j = 0; j < t; j++)
                {
                    sum += pTilde[j] * v[j, a];
                }

                ev[a] = sum;
            }

            var grad = new double[k];
            var gradMax = 0.0;
            for (var a = 0; a < k; a++)
            {
                grad[a] = ev[a] - b[a];
                gradMax = Math.Max(gradMax, Math.Abs(grad[a]));
            }

            lastGradMax = gradMax;

            if (gradMax < tol)
            {
                break;
            }

            // Hessian = Cov_p̃[V] (PSD). Newton descent: dλ = Cov⁻¹ (b − E[V]).
            var cov = new double[k, k];
            for (var a = 0; a < k; a++)
            {
                for (var bb = a; bb < k; bb++)
                {
                    var sum = 0.0;
                    for (var j = 0; j < t; j++)
                    {
                        sum += pTilde[j] * (v[j, a] - ev[a]) * (v[j, bb] - ev[bb]);
                    }

                    cov[a, bb] = sum;
                    cov[bb, a] = sum;
                }
            }

            for (var a = 0; a < k; a++)
            {
                cov[a, a] += 1e-15;
            }

            var rhs = new double[k];
            for (var a = 0; a < k; a++)
            {
                rhs[a] = b[a] - ev[a];
            }

            double[] dlam;
            try
            {
                dlam = SolveLinearSystem(cov, rhs, k);
            }
            catch (CalculationException)
            {
                // Hessian singular but gradient non-zero ⇒ the view direction is degenerate
                // / infeasible (no probability reweighting reduces the residual).
                throw new CalculationException(
                    "Entropy Pooling dual Hessian is singular while the view residual is non-zero; " +
                    "the views are inconsistent with the prior support.");
            }

            // Backtracking line search on the convex dual (accept the largest descent step).
            var baseObj = DualObjective(p, v, b, lam, t, k);
            var step = 1.0;
            var newLam = new double[k];
            var accepted = false;
            for (var bt = 0; bt < 60; bt++)
            {
                for (var a = 0; a < k; a++)
                {
                    newLam[a] = lam[a] + step * dlam[a];
                }

                if (DualObjective(p, v, b, newLam, t, k) < baseObj)
                {
                    accepted = true;
                    break;
                }

                step *= 0.5;
            }

            if (!accepted)
            {
                // No descent step found but the view residual is still large ⇒ the demanded
                // expectation lies outside the convex hull of the scenario view-scores: infeasible.
                if (gradMax > 1e-6)
                {
                    throw new CalculationException(
                        "Entropy Pooling views are infeasible: the demanded posterior expectation " +
                        "lies outside the convex hull of the scenario view-scores. " +
                        $"Residual = {gradMax:G6}.");
                }

                break;
            }

            Array.Copy(newLam, lam, k);
        }

        // Feasibility check: if the view residual never collapsed (E_p̃[V] saturated at the
        // convex-hull boundary of the scenario view-scores while λ ran away), the demanded
        // expectation is outside the prior support — no KL solution exists (Meucci 2010 accuracy
        // contract: a unique KL solution exists only when views are consistent with the prior).
        if (lastGradMax > 1e-6)
        {
            throw new CalculationException(
                "Entropy Pooling views are infeasible: the demanded posterior expectation " +
                "lies outside the convex hull of the scenario view-scores (no KL-consistent " +
                $"posterior exists). Residual = {lastGradMax:G6}.");
        }

        return PosteriorFromLambda(p, v, lam, t, k);
    }

    private static double[] PosteriorFromLambda(double[] p, double[,] v, double[] lam, int t, int k)
    {
        var logits = new double[t];
        var maxLogit = double.NegativeInfinity;
        for (var j = 0; j < t; j++)
        {
            var vl = 0.0;
            for (var a = 0; a < k; a++)
            {
                vl += v[j, a] * lam[a];
            }

            logits[j] = Math.Log(p[j]) + vl;
            maxLogit = Math.Max(maxLogit, logits[j]);
        }

        var sum = 0.0;
        var w = new double[t];
        for (var j = 0; j < t; j++)
        {
            w[j] = Math.Exp(logits[j] - maxLogit);
            sum += w[j];
        }

        for (var j = 0; j < t; j++)
        {
            w[j] /= sum;
        }

        return w;
    }

    private static double DualObjective(double[] p, double[,] v, double[] b, double[] lam, int t, int k)
    {
        // D(λ) = log( Σ_j p_j exp(λᵀ V_j) ) − λᵀ b, computed with a max-shift for stability.
        var maxLogit = double.NegativeInfinity;
        var logits = new double[t];
        for (var j = 0; j < t; j++)
        {
            var vl = 0.0;
            for (var a = 0; a < k; a++)
            {
                vl += v[j, a] * lam[a];
            }

            logits[j] = Math.Log(p[j]) + vl;
            maxLogit = Math.Max(maxLogit, logits[j]);
        }

        var sumExp = 0.0;
        for (var j = 0; j < t; j++)
        {
            sumExp += Math.Exp(logits[j] - maxLogit);
        }

        var logSumExp = maxLogit + Math.Log(sumExp);

        var lamB = 0.0;
        for (var a = 0; a < k; a++)
        {
            lamB += lam[a] * b[a];
        }

        return logSumExp - lamB;
    }

    // ─── Mean-variance step — reuse MeanVarianceConstruction machinery ──────

    /// <summary>
    /// Feeds the posterior moments (μ̃, Σ̃) to <see cref="MeanVarianceConstruction"/> — the
    /// spec's required optimizer. A synthetic 2-observation series whose per-asset average
    /// equals μ̃, paired with a fixed-Σ̃ covariance estimator, drives the optimizer so the
    /// only signal is the EP posterior moments.
    /// </summary>
    private IReadOnlyDictionary<Symbol, decimal> MeanVarianceOnPosterior(
        IReadOnlyList<Symbol> assets, decimal[] mu, decimal[,] sigma)
    {
        var n = mu.Length;
        var synthetic = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            synthetic[i] = [mu[i] + 0.01m, mu[i] - 0.01m];
        }

        var mv = new MeanVarianceConstruction(
            covarianceEstimator: new FixedCovarianceEstimator(sigma),
            minWeight: _minWeight,
            maxWeight: _maxWeight,
            riskAversion: _riskAversion);

        return mv.ComputeTargetWeights(assets, synthetic);
    }

    /// <summary>A covariance estimator that always returns the supplied posterior covariance Σ̃.</summary>
    private sealed class FixedCovarianceEstimator(decimal[,] fixedCov) : ICovarianceEstimator
    {
        public decimal[,] Estimate(decimal[][] returns) => (decimal[,])fixedCov.Clone();
    }

    // ─── Small linear solver (Gauss-Jordan with partial pivoting) ───────────

    private static double[] SolveLinearSystem(double[,] matrix, double[] rhs, int size)
    {
        var a = new double[size, size + 1];
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                a[i, j] = matrix[i, j];
            }

            a[i, size] = rhs[i];
        }

        for (var col = 0; col < size; col++)
        {
            var maxRow = col;
            for (var row = col + 1; row < size; row++)
            {
                if (Math.Abs(a[row, col]) > Math.Abs(a[maxRow, col]))
                {
                    maxRow = row;
                }
            }

            if (maxRow != col)
            {
                for (var j = 0; j <= size; j++)
                {
                    (a[col, j], a[maxRow, j]) = (a[maxRow, j], a[col, j]);
                }
            }

            if (Math.Abs(a[col, col]) < 1e-300)
            {
                throw new CalculationException("Matrix is singular and cannot be inverted.");
            }

            var pivot = a[col, col];
            for (var j = 0; j <= size; j++)
            {
                a[col, j] /= pivot;
            }

            for (var row = 0; row < size; row++)
            {
                if (row == col)
                {
                    continue;
                }

                var factor = a[row, col];
                for (var j = 0; j <= size; j++)
                {
                    a[row, j] -= factor * a[col, j];
                }
            }
        }

        var result = new double[size];
        for (var i = 0; i < size; i++)
        {
            result[i] = a[i, size];
        }

        return result;
    }
}
