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

using Boutquin.Numerics.Solvers;
using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Domain.Exceptions;

/// <summary>
/// Sparse (ℓ₁-regularized) minimum-variance portfolio — Husmann, Shivarova &amp;
/// Steinert (2019), <i>"Sparsity and Stability for Minimum-Variance Portfolios"</i>
/// (arXiv 1910.11840), <b>Model 2 (LASSO)</b>:
/// <code>
///   min_w  wᵀΣ̂w   s.t.   1ᵀw = 1,   ‖w‖₁ ≤ δ.
/// </code>
/// The ℓ₁ (gross-exposure / leverage) budget <c>δ</c> caps the sum of absolute weights,
/// shrinking the long-short positions a plain min-var would otherwise take and yielding a
/// sparser, more stable allocation that is robust to covariance-estimation error.
/// </summary>
/// <remarks>
/// <para><b>Augmented-QP form.</b> Per the reference, the ℓ₁ budget is handled by the
/// augmented variables <c>w = w⁺ − w⁻</c>, <c>w⁺, w⁻ ≥ 0</c>, on the
/// <c>[[Σ̂, −Σ̂], [−Σ̂, Σ̂]]</c> objective (so the augmented quadratic form equals
/// <c>wᵀΣ̂w</c>) with the gross-exposure budget <c>1ᵀ(w⁺ + w⁻) ≤ δ</c>. The convex QP
/// (PSD objective, linear constraints) is globally solvable. This implementation solves it
/// from the equivalent closed-form active set rather than iterating the doubled variable
/// set, which keeps the optimum exact and reproducible cross-language (no optimizer-path
/// drift): for a fixed optimal sign pattern <c>s = sign(w)</c> the ℓ₁ constraint is the
/// linear equality <c>sᵀw = δ</c>, and the two-equality KKT system
/// <c>min wᵀΣ̂w s.t. 1ᵀw = 1, sᵀw = δ</c> has a closed form. The sign pattern is found by an
/// active-set sweep seeded from the unconstrained optimum.</para>
///
/// <para><b>The defining reduction (fidelity property).</b> Because V1 ships Model 2 only
/// (no turnover budget <c>k</c>), the budget binds on <c>δ</c> alone:
/// <list type="bullet">
///   <item><description><c>δ ≥ ‖w*‖₁</c> (in particular <c>δ = ∞</c>): the budget is slack
///   and the optimum is the <b>unconstrained</b> sum-to-one minimum variance
///   <c>w* = Σ̂⁻¹·1 / (1ᵀΣ̂⁻¹·1)</c> — i.e. it reduces to the standard minimum-variance
///   portfolio (<see cref="MinimumVarianceConstruction"/>) whenever that interior optimum is
///   long-only.</description></item>
///   <item><description><c>δ = 1</c>: the long-only corner. For a sum-to-one portfolio
///   <c>‖w‖₁ ≥ |1ᵀw| = 1</c> with equality iff every weight is non-negative, so the
///   <c>δ = 1</c> optimum equals the long-only minimum variance
///   (<c>ActiveSetQpSolver.SolveMinVariance</c> with lower bound 0). This is the default and
///   guarantees the long-only / non-negative invariant of
///   <see cref="IPortfolioConstructionModel"/>.</description></item>
/// </list></para>
///
/// <para><b>Accuracy contract:</b> <c>δ ≥ 1</c> is required for a long-only feasible
/// solution under the budget constraint; <c>δ &lt; 1</c> is infeasible (the gross exposure of
/// a sum-to-one portfolio can never drop below 1) and is rejected with
/// <see cref="CalculationException"/>. For a positive-definite Σ̂ the QP is convex and
/// globally solvable; pre-condition with RMT-denoising / Ledoit-Wolf shrinkage at high N/T by
/// injecting the corresponding <see cref="ICovarianceEstimator"/>.</para>
///
/// <para><b>Intended omissions (V1):</b> ℓ₀ cardinality (NP-hard, needs MIP); built-in
/// cross-validation of <c>δ</c> (<c>δ</c> is exposed as a parameter); the turnover-budgeted
/// <b>Model 3</b> (per-step budget <c>k</c>, <c>w ≤ k + w_a</c>) — a natural v2.</para>
/// </remarks>
public sealed class SparseMinimumVarianceConstruction : IPortfolioConstructionModel
{
    private const decimal Tolerance = 1e-12m;

    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _delta;

    /// <summary>
    /// Initializes a new instance of the <see cref="SparseMinimumVarianceConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to
    /// <see cref="SampleCovarianceEstimator"/>. Inject a denoising / shrinkage estimator when
    /// N/T is high so Σ̂ stays invertible.</param>
    /// <param name="delta">The ℓ₁ (gross-exposure) budget <c>δ ≥ 1</c>. <c>δ = 1</c> (default)
    /// is the long-only corner; <c>δ = decimal.MaxValue</c> is the unconstrained min-var
    /// (standard §13 reduction); intermediate <c>δ</c> caps gross exposure.</param>
    /// <exception cref="CalculationException">Thrown when <c>δ &lt; 1</c> — infeasible for a
    /// long-only sum-to-one portfolio.</exception>
    public SparseMinimumVarianceConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal delta = 1.0m)
    {
        if (delta < 1m - Tolerance)
        {
            throw new CalculationException(
                $"SparseMinimumVarianceConstruction requires an ℓ₁ budget δ ≥ 1 for a long-only " +
                $"feasible solution (gross exposure of a sum-to-one portfolio is always ≥ 1); got δ = {delta}.");
        }

        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _delta = delta;
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

        var n = assets.Count;
        if (n == 1)
        {
            return new Dictionary<Symbol, decimal> { [assets[0]] = 1m };
        }

        var cov = _covarianceEstimator.Estimate(returns);
        var w = SolveSparse(cov, _delta, n);

        var weights = new Dictionary<Symbol, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            weights[assets[i]] = w[i];
        }

        return weights;
    }

    /// <summary>
    /// Solves the Model-2 LASSO min-var QP for a given ℓ₁ budget <c>δ</c>.
    /// </summary>
    private static decimal[] SolveSparse(decimal[,] cov, decimal delta, int n)
    {
        // Unconstrained (sum-to-one) min-var: w_u = Σ⁻¹·1 / (1ᵀΣ⁻¹·1). When its gross
        // exposure is within the budget (δ ≥ ‖w_u‖₁, in particular δ = ∞) the budget is
        // slack and the unconstrained optimum is the answer — the standard-min-var reduction.
        decimal[]? wUnconstrained = TrySolveUnconstrained(cov, n);

        if (wUnconstrained is not null)
        {
            var grossUnc = GrossExposure(wUnconstrained);
            if (delta >= grossUnc - Tolerance)
            {
                return wUnconstrained;
            }
        }

        // δ = 1 is the long-only corner: ‖w‖₁ = 1 with Σw = 1 ⟺ all weights ≥ 0. Delegate to
        // the tested long-only min-var solver (lower bound 0), which also covers the
        // non-PSD / boundary cases the closed-form path cannot.
        if (delta <= 1m + Tolerance)
        {
            return SolveLongOnlyMinVar(cov, n);
        }

        // 1 < δ < ‖w_u‖₁: the budget binds. Active-set sweep over the optimal sign pattern,
        // seeded from the unconstrained signs (the correct warm start when relaxing from the
        // unconstrained optimum toward the budget). For a fixed sign vector s the ℓ₁ budget is
        // the linear equality sᵀw = δ, and the two-equality KKT has a closed form.
        if (wUnconstrained is null)
        {
            // Σ not invertible — fall back to the long-only solver (cannot exceed budget anyway).
            return SolveLongOnlyMinVar(cov, n);
        }

        var sign = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            sign[i] = wUnconstrained[i] >= 0m ? 1m : -1m;
        }

        decimal[]? last = null;
        for (var sweep = 0; sweep < 8 * n + 8; sweep++)
        {
            var w = SolveTwoEqualityKkt(cov, sign, delta, n);
            if (w is null)
            {
                break;
            }

            var consistent = true;
            for (var i = 0; i < n; i++)
            {
                var newSign = w[i] >= 0m ? 1m : -1m;
                if (newSign != sign[i])
                {
                    sign[i] = newSign;
                    consistent = false;
                }
            }

            if (consistent)
            {
                return w;
            }

            last = w;
        }

        // Did not settle on a sign pattern — return the best feasible iterate, or the
        // long-only corner as a guaranteed-feasible fallback.
        return last ?? SolveLongOnlyMinVar(cov, n);
    }

    /// <summary>Long-only minimum variance (lb = 0, ub = 1) via the tested active-set solver,
    /// with the same non-PSD gradient-descent fallback as <see cref="MinimumVarianceConstruction"/>.</summary>
    private static decimal[] SolveLongOnlyMinVar(decimal[,] cov, int n)
    {
        try
        {
            return ActiveSetQpSolver<decimal>.SolveMinVariance(cov, 0m, 1m);
        }
        catch (InvalidOperationException)
        {
            return SolveGradientDescent(cov, n);
        }
    }

    /// <summary>
    /// Unconstrained sum-to-one min-var: <c>w = Σ⁻¹·1 / (1ᵀΣ⁻¹·1)</c>. Returns
    /// <see langword="null"/> when Σ is singular (no invertible solution).
    /// </summary>
    private static decimal[]? TrySolveUnconstrained(decimal[,] cov, int n)
    {
        var ones = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            ones[i] = 1m;
        }

        var z = SolveLinear(cov, ones, n);
        if (z is null)
        {
            return null;
        }

        var sumZ = 0m;
        for (var i = 0; i < n; i++)
        {
            sumZ += z[i];
        }

        if (Math.Abs(sumZ) < Tolerance)
        {
            return null;
        }

        for (var i = 0; i < n; i++)
        {
            z[i] /= sumZ;
        }

        return z;
    }

    /// <summary>
    /// Closed-form optimum of <c>min wᵀΣw s.t. 1ᵀw = 1, sᵀw = δ</c> for a fixed sign vector s.
    /// KKT: <c>2Σw = ν·1 + μ·s</c>. With <c>p = Σ⁻¹·1</c>, <c>q = Σ⁻¹·s</c>:
    /// <c>w = (ν·p + μ·q)/2</c> where <c>[[1ᵀp,1ᵀq],[sᵀp,sᵀq]]·[ν;μ]/2 = [1;δ]</c>.
    /// Returns <see langword="null"/> when Σ or the 2×2 KKT matrix is singular.
    /// </summary>
    private static decimal[]? SolveTwoEqualityKkt(decimal[,] cov, decimal[] sign, decimal delta, int n)
    {
        var ones = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            ones[i] = 1m;
        }

        var p = SolveLinear(cov, ones, n);
        var q = SolveLinear(cov, sign, n);
        if (p is null || q is null)
        {
            return null;
        }

        // 2×2 system M·[ν;μ] = [1;δ] with M = 0.5·[[1ᵀp,1ᵀq],[sᵀp,sᵀq]].
        decimal onesP = 0m, onesQ = 0m, signP = 0m, signQ = 0m;
        for (var i = 0; i < n; i++)
        {
            onesP += p[i];
            onesQ += q[i];
            signP += sign[i] * p[i];
            signQ += sign[i] * q[i];
        }

        var a11 = onesP / 2m;
        var a12 = onesQ / 2m;
        var a21 = signP / 2m;
        var a22 = signQ / 2m;

        var det = (a11 * a22) - (a12 * a21);
        if (Math.Abs(det) < Tolerance)
        {
            return null;
        }

        var nu = ((a22 * 1m) - (a12 * delta)) / det;
        var mu = ((a11 * delta) - (a21 * 1m)) / det;

        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = ((nu * p[i]) + (mu * q[i])) / 2m;
        }

        return w;
    }

    private static decimal GrossExposure(decimal[] w)
    {
        var gross = 0m;
        foreach (var wi in w)
        {
            gross += Math.Abs(wi);
        }

        return gross;
    }

    /// <summary>
    /// Solves the linear system <c>A·x = b</c> for a symmetric A via Gaussian elimination
    /// with partial pivoting. Returns <see langword="null"/> when A is singular.
    /// </summary>
    private static decimal[]? SolveLinear(decimal[,] a, decimal[] b, int n)
    {
        // Work on copies (augmented matrix).
        var m = new decimal[n, n];
        var rhs = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            rhs[i] = b[i];
            for (var j = 0; j < n; j++)
            {
                m[i, j] = a[i, j];
            }
        }

        for (var col = 0; col < n; col++)
        {
            // Partial pivot: largest-magnitude entry in this column at or below the diagonal.
            var pivotRow = col;
            var pivotMag = Math.Abs(m[col, col]);
            for (var r = col + 1; r < n; r++)
            {
                var mag = Math.Abs(m[r, col]);
                if (mag > pivotMag)
                {
                    pivotMag = mag;
                    pivotRow = r;
                }
            }

            if (pivotMag < Tolerance)
            {
                return null;
            }

            if (pivotRow != col)
            {
                for (var j = 0; j < n; j++)
                {
                    (m[col, j], m[pivotRow, j]) = (m[pivotRow, j], m[col, j]);
                }

                (rhs[col], rhs[pivotRow]) = (rhs[pivotRow], rhs[col]);
            }

            var pivot = m[col, col];
            for (var r = col + 1; r < n; r++)
            {
                var factor = m[r, col] / pivot;
                if (factor == 0m)
                {
                    continue;
                }

                for (var j = col; j < n; j++)
                {
                    m[r, j] -= factor * m[col, j];
                }

                rhs[r] -= factor * rhs[col];
            }
        }

        // Back-substitution.
        var x = new decimal[n];
        for (var i = n - 1; i >= 0; i--)
        {
            var sum = rhs[i];
            for (var j = i + 1; j < n; j++)
            {
                sum -= m[i, j] * x[j];
            }

            x[i] = sum / m[i, i];
        }

        return x;
    }

    /// <summary>
    /// Projected gradient descent fallback for the long-only min-var when the active-set
    /// solver reports a non-positive-definite covariance. Mirrors
    /// <see cref="MinimumVarianceConstruction"/>'s fallback exactly.
    /// </summary>
    private static decimal[] SolveGradientDescent(decimal[,] cov, int n)
    {
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m / n;
        }

        for (var iter = 0; iter < 5000; iter++)
        {
            var grad = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    grad[i] += 2m * cov[i, j] * w[j];
                }
            }

            var stepped = false;
            var currentLr = 1.0m;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    candidate[i] = w[i] - currentLr * grad[i];
                }

                ProjectOntoSimplex(candidate);
                if (ComputePortfolioVariance(candidate, cov) < ComputePortfolioVariance(w, cov))
                {
                    var maxDiff = 0m;
                    for (var i = 0; i < n; i++)
                    {
                        maxDiff = Math.Max(maxDiff, Math.Abs(candidate[i] - w[i]));
                    }

                    w = candidate;
                    stepped = true;
                    if (maxDiff < Tolerance)
                    {
                        return w;
                    }

                    break;
                }

                currentLr *= 0.5m;
            }

            if (!stepped)
            {
                break;
            }
        }

        return w;
    }

    private static decimal ComputePortfolioVariance(decimal[] w, decimal[,] cov)
    {
        var n = w.Length;
        var variance = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                variance += w[i] * w[j] * cov[i, j];
            }
        }

        return variance;
    }

    private static void ProjectOntoSimplex(decimal[] w)
    {
        var n = w.Length;
        for (var round = 0; round < 50; round++)
        {
            for (var i = 0; i < n; i++)
            {
                w[i] = Math.Max(0m, Math.Min(1m, w[i]));
            }

            var sum = w.Sum();
            if (sum <= 0m)
            {
                for (var i = 0; i < n; i++)
                {
                    w[i] = 1m / n;
                }

                return;
            }

            for (var i = 0; i < n; i++)
            {
                w[i] /= sum;
            }

            var feasible = true;
            for (var i = 0; i < n; i++)
            {
                if (w[i] < -1e-14m || w[i] > 1m + 1e-14m)
                {
                    feasible = false;
                    break;
                }
            }

            if (feasible)
            {
                return;
            }
        }
    }
}
