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
using Boutquin.Trading.Domain.Helpers;
using Domain.ValueObjects;

/// <summary>
/// Maximizes the diversification ratio DR(w) = Σ(w_i × σ_i) / σ_portfolio
/// (Chopin &amp; Briand, 2008).
///
/// Implementation: reformulates as minimum-variance on volatility-normalized returns.
/// If z_i = r_i / σ_i, then minimizing z'Σ_z z with simplex constraints on the
/// normalized weights, then un-normalizing back to original space, maximizes DR.
/// Uses projected gradient descent with line search, identical to MinimumVarianceConstruction.
///
/// When all assets are equally correlated, degenerates gracefully to inverse-volatility.
/// </summary>
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

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns)
    {
        Guard.AgainstNull(() => assets);

        if (assets.Count == 0)
        {
            return new Dictionary<Asset, decimal>();
        }

        if (returns is null || returns.Length != assets.Count)
        {
            throw new ArgumentException("Returns array must have one series per asset.", nameof(returns));
        }

        var n = assets.Count;

        if (n == 1)
        {
            return new Dictionary<Asset, decimal> { [assets[0]] = 1m };
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
            y = CholeskyQpSolver.SolveMinVarianceQP(corr, n, 0m, 1.0m);
        }
        catch (CalculationException)
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
            // Re-solve with outer bounds: the constrained MaxDiv is equivalent to
            // constrained MinVar on the correlation matrix with transformed bounds.
            // Since the transform w_i = (y_i/σ_i)/Σ(y_j/σ_j) is monotonic,
            // we can solve the constrained problem by passing bounds to the inner solver.
            // However, the exact bound transform is non-trivial, so we use MinVar on
            // the original covariance with the outer bounds as a good approximation.
            try
            {
                rawW = CholeskyQpSolver.SolveMinVarianceQP(cov, n, _minWeight, _maxWeight);
            }
            catch (CalculationException)
            {
                ProjectOntoSimplex(rawW, _minWeight, _maxWeight);
            }
        }

        var weights = new Dictionary<Asset, decimal>(n);
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
