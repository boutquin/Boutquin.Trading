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
/// Maximizes the diversification ratio DR(w) = Σ(w_i × σ_i) / σ_portfolio
/// (Choueifaty &amp; Coignard, 2008).
///
/// For distinct assets, symmetric positive-definite covariance and feasible uniform bounds
/// in [0,1], certifies exact budget/bounds and true diversification-ratio relative loss at most
/// 1e-10. Uses the existing correlation-QP fast path, then at most 256 asset-space
/// mean-variance proposals, with an exact rational global certificate on every returned vector.
/// </summary>
/// <remarks>
/// The supported route refuses unresolved numerical results with <see cref="InvalidOperationException"/>.
/// Empty/singleton behavior and legacy routes for non-SPD covariance, duplicate assets and
/// nonstandard/infeasible bounds are retained; those legacy routes do not have the certified
/// optimality guarantee, and relaxed infeasible bounds do not satisfy authored hard constraints.
/// Exact SPD classification costs O(N³) rational operations; each certificate uses O(N²)
/// matrix work and O(N log N) comparisons. Big-integer costs depend on input bit lengths.
/// All solver state is per call; concurrent use requires a thread-safe covariance estimator.
/// </remarks>
public sealed class MaximumDiversificationConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;
    private readonly int _maxIterations;
    private readonly decimal _tolerance;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaximumDiversificationConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    /// <param name="maxIterations">Maximum gradient descent iterations. Default 5000.</param>
    /// <param name="tolerance">Convergence tolerance. Default 1e-12.</param>
    public MaximumDiversificationConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m,
        int maxIterations = 5000,
        decimal tolerance = 1e-12m)
    {
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _minWeight = minWeight;
        _maxWeight = maxWeight;
        _maxIterations = maxIterations;
        _tolerance = tolerance;
    }

    /// <summary>Computes diversification weights in the supplied asset order.</summary>
    /// <param name="assets">Ordered asset symbols; distinct symbols are required by the certified route.</param>
    /// <param name="returns">One return series per asset, passed to the configured covariance estimator.</param>
    /// <returns>Certified weights for supported calls, or the retained legacy result for external-domain calls.</returns>
    /// <exception cref="ArgumentNullException">The asset list is null.</exception>
    /// <exception cref="ArgumentException">A nonempty asset list has null returns or a different series count.</exception>
    /// <exception cref="CalculationException">An asset has zero volatility.</exception>
    /// <exception cref="OverflowException">The legacy diagonal-volatility conversion is not representable.</exception>
    /// <exception cref="InvalidOperationException">A supported numerical calculation is unresolved or no
    /// emitted vector meets the universal true-DR certificate within the fixed work limit.</exception>
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

        // Extract individual volatilities from diagonal of covariance matrix
        var vols = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            vols[i] = (decimal)Math.Sqrt((double)cov[i, i]);
            if (vols[i] <= 0m)
            {
                throw new CalculationException(
                    $"Volatility is zero for asset {assets[i]}; cannot compute maximum diversification weight.");
            }
        }

        if (assets.Distinct().Count() == n &&
            CertifiedMaximumDiversification.TrySolve(cov, vols, _minWeight, _maxWeight, out var certified, out _))
        {
            var result = new Dictionary<Symbol, decimal>(n);
            for (var i = 0; i < n; i++)
            {
                result.Add(assets[i], certified[i]);
            }

            return result;
        }

        // Build correlation matrix: corr[i,j] = cov[i,j] / (σ_i * σ_j)
        // This is the covariance matrix of volatility-normalized returns
        var corr = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                corr[i, j] = cov[i, j] / (vols[i] * vols[j]);
            }
        }

        // Solve MinVar on the correlation matrix: find y that minimizes y'C y
        // subject to Σy_i = 1 and y_i ≥ 0
        decimal[] y;
        try
        {
            y = ActiveSetQpSolver.SolveMinVariance(corr, 0m, 1.0m);
        }
        catch (InvalidOperationException)
        {
            // Fallback to gradient descent for non-positive-definite correlation matrices
            y = SolveMinVarGradientDescent(corr, n);
        }

        // Un-normalize: w_i = (y_i / σ_i) / Σ(y_j / σ_j)
        var rawW = new decimal[n];
        var sumRaw = 0m;
        for (var i = 0; i < n; i++)
        {
            rawW[i] = y[i] / vols[i];
            sumRaw += rawW[i];
        }

        for (var i = 0; i < n; i++)
        {
            rawW[i] /= sumRaw;
        }

        // Check if unconstrained weights satisfy outer bounds
        var outerMin = Math.Min(_minWeight, 1m / n);
        var outerMax = Math.Max(_maxWeight, 1m / n);
        var needsProjection = false;
        for (var i = 0; i < n; i++)
        {
            if (rawW[i] < outerMin - 1e-14m || rawW[i] > outerMax + 1e-14m)
            {
                needsProjection = true;
                break;
            }
        }

        if (needsProjection)
        {
            // Constrained maximum-diversification: keep optimizing the diversification
            // objective (MinVar on the *correlation* matrix, i.e. on volatility-normalized
            // returns) under the user bounds, then un-normalize back to asset space and
            // project onto the asset-space bounds. Solving MinVar on the *covariance*
            // matrix here would silently swap the objective to minimum-variance, which
            // is a different optimum whenever asset volatilities differ.
            decimal[] yc;
            try
            {
                yc = ActiveSetQpSolver.SolveMinVariance(corr, _minWeight, _maxWeight);
            }
            catch (InvalidOperationException)
            {
                yc = SolveMinVarGradientDescent(corr, n);
            }

            var sumC = 0m;
            for (var i = 0; i < n; i++)
            {
                rawW[i] = yc[i] / vols[i];
                sumC += rawW[i];
            }

            if (sumC > 0m)
            {
                for (var i = 0; i < n; i++)
                {
                    rawW[i] /= sumC;
                }
            }

            // Enforce the asset-space bounds the un-normalization may have breached.
            ProjectOntoCappedSimplex(rawW, _minWeight, _maxWeight);
        }

        var weights = new Dictionary<Symbol, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            weights[assets[i]] = rawW[i];
        }

        return weights;
    }

    private decimal[] SolveMinVarGradientDescent(decimal[,] cov, int n)
    {
        // Projected gradient descent — same as MinimumVarianceConstruction
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m / n;
        }

        for (var iter = 0; iter < _maxIterations; iter++)
        {
            // Gradient of w'Σw = 2Σw
            var grad = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    grad[i] += 2m * cov[i, j] * w[j];
                }
            }

            // Line search
            var stepped = false;
            var currentLr = 1.0m;

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    candidate[i] = w[i] - currentLr * grad[i];
                }

                ProjectOntoSimplex(candidate, 0m, 1.0m);

                var newVar = ComputeQuadratic(candidate, cov);
                var oldVar = ComputeQuadratic(w, cov);

                if (newVar < oldVar)
                {
                    var maxDiff = 0m;
                    for (var i = 0; i < n; i++)
                    {
                        maxDiff = Math.Max(maxDiff, Math.Abs(candidate[i] - w[i]));
                    }

                    w = candidate;
                    stepped = true;

                    if (maxDiff < _tolerance)
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

    private static decimal ComputeQuadratic(decimal[] w, decimal[,] mat)
    {
        var n = w.Length;
        var result = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                result += w[i] * w[j] * mat[i, j];
            }
        }

        return result;
    }

    /// <summary>
    /// Projects <paramref name="w"/> onto { Σw_i = 1, minWeight ≤ w_i ≤ maxWeight } using
    /// waterfilling: saturate the bounds that bind, fix those coordinates, then redistribute
    /// the residual mass proportionally across the still-free coordinates until none breach.
    /// Unlike the clamp-then-renormalize loop, the saturated coordinates stay fixed at the
    /// bound, so the returned vector satisfies the box constraints to within rounding.
    /// A final headroom-redistribution pass absorbs any residual mass left when a single round
    /// saturates every free coordinate at once (one to maxWeight, another to minWeight), which
    /// would otherwise leave 1 − Σw unallocated and break the sum-to-1.0 contract.
    /// </summary>
    private static void ProjectOntoCappedSimplex(decimal[] w, decimal minWeight, decimal maxWeight)
    {
        var n = w.Length;

        // Auto-relax constraints when infeasible: with N assets, maxWeight must be >= 1/N
        // and minWeight must be <= 1/N, otherwise weights can't sum to 1.0.
        maxWeight = Math.Max(maxWeight, 1m / n);
        minWeight = Math.Min(minWeight, 1m / n);

        // Start from the renormalized non-negative vector.
        for (var i = 0; i < n; i++)
        {
            w[i] = Math.Max(0m, w[i]);
        }

        var total = w.Sum();
        if (total <= 0m)
        {
            for (var i = 0; i < n; i++)
            {
                w[i] = 1m / n;
            }
        }
        else
        {
            for (var i = 0; i < n; i++)
            {
                w[i] /= total;
            }
        }

        var fixedFlags = new bool[n];

        for (var round = 0; round < n + 1; round++)
        {
            // Mass already committed to saturated coordinates, and the free pool's sum.
            var fixedMass = 0m;
            var freeSum = 0m;
            var freeCount = 0;
            for (var i = 0; i < n; i++)
            {
                if (fixedFlags[i])
                {
                    fixedMass += w[i];
                }
                else
                {
                    freeSum += w[i];
                    freeCount++;
                }
            }

            var freeTarget = 1m - fixedMass;
            if (freeCount == 0)
            {
                break;
            }

            // Scale the free coordinates to absorb the residual mass.
            if (freeSum > 0m)
            {
                var scale = freeTarget / freeSum;
                for (var i = 0; i < n; i++)
                {
                    if (!fixedFlags[i])
                    {
                        w[i] *= scale;
                    }
                }
            }
            else
            {
                var equalShare = freeTarget / freeCount;
                for (var i = 0; i < n; i++)
                {
                    if (!fixedFlags[i])
                    {
                        w[i] = equalShare;
                    }
                }
            }

            // Saturate any free coordinate that now breaches a bound and fix it.
            var changed = false;
            for (var i = 0; i < n; i++)
            {
                if (fixedFlags[i])
                {
                    continue;
                }

                if (w[i] > maxWeight)
                {
                    w[i] = maxWeight;
                    fixedFlags[i] = true;
                    changed = true;
                }
                else if (w[i] < minWeight)
                {
                    w[i] = minWeight;
                    fixedFlags[i] = true;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        // A waterfilling round can saturate every free coordinate at once — one to maxWeight,
        // another to minWeight — so the loop exits with freeCount==0 while residual mass remains.
        // Pour that residual into the coordinates that still have headroom toward the unsaturated
        // bound. A feasible point exists whenever n·maxWeight ≥ 1 ≥ n·minWeight (guaranteed by the
        // auto-relax above), so headroom is always available while |Σw − 1| > tolerance.
        for (var round = 0; round < n + 1; round++)
        {
            var residual = 1m - w.Sum();
            if (Math.Abs(residual) <= 1e-14m)
            {
                break;
            }

            // Total headroom in the direction we need to move: toward max if under-allocated,
            // toward min if over-allocated.
            var headroom = 0m;
            for (var i = 0; i < n; i++)
            {
                headroom += residual > 0m ? maxWeight - w[i] : w[i] - minWeight;
            }

            if (headroom <= 0m)
            {
                break;
            }

            // Distribute the residual proportionally to each coordinate's available headroom,
            // clamping at the bound so no coordinate breaches the box.
            for (var i = 0; i < n; i++)
            {
                var coordHeadroom = residual > 0m ? maxWeight - w[i] : w[i] - minWeight;
                var delta = residual * (coordHeadroom / headroom);
                w[i] += delta;
                w[i] = Math.Max(minWeight, Math.Min(maxWeight, w[i]));
            }
        }
    }

    private static void ProjectOntoSimplex(decimal[] w, decimal minWeight, decimal maxWeight)
    {
        var n = w.Length;

        // Auto-relax constraints when infeasible: with N assets, maxWeight must be >= 1/N
        // and minWeight must be <= 1/N, otherwise weights can't sum to 1.0.
        maxWeight = Math.Max(maxWeight, 1m / n);
        minWeight = Math.Min(minWeight, 1m / n);

        for (var round = 0; round < 50; round++)
        {
            for (var i = 0; i < n; i++)
            {
                w[i] = Math.Max(minWeight, Math.Min(maxWeight, w[i]));
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
                if (w[i] < minWeight - 1e-14m || w[i] > maxWeight + 1e-14m)
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
