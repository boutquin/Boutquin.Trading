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
using Domain.ValueObjects;

/// <summary>
/// Computes the minimum-variance portfolio: minimizes w'Σw subject to
/// Σw_i = 1 and w_i ≥ 0 (long-only).
/// Uses projected gradient descent with line search.
/// </summary>
public sealed class MinimumVarianceConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly int _maxIterations;
    private readonly decimal _tolerance;

    /// <summary>
    /// Initializes a new instance of the <see cref="MinimumVarianceConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="maxIterations">Maximum gradient descent iterations. Default 5000.</param>
    /// <param name="tolerance">Convergence tolerance. Default 1e-12.</param>
    public MinimumVarianceConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        int maxIterations = 5000,
        decimal tolerance = 1e-12m)
    {
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
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
        var cov = _covarianceEstimator.Estimate(returns);

        // Start with equal weights
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m / n;
        }

        var learningRate = 1.0m;

        var converged = false;

        for (var iter = 0; iter < _maxIterations && !converged; iter++)
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
            var currentLr = learningRate;

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    candidate[i] = w[i] - currentLr * grad[i];
                }

                // Project onto simplex (non-negative, sum to 1)
                ProjectOntoSimplex(candidate);

                var newVar = ComputePortfolioVariance(candidate, cov);
                var oldVar = ComputePortfolioVariance(w, cov);

                if (newVar < oldVar)
                {
                    var maxDiff = 0m;
                    for (var i = 0; i < n; i++)
                    {
                        maxDiff = Math.Max(maxDiff, Math.Abs(candidate[i] - w[i]));
                    }

                    w = candidate;
                    stepped = true;
                    converged = maxDiff < _tolerance;
                    break;
                }

                currentLr *= 0.5m;
            }

            if (!stepped)
            {
                break;
            }
        }

        var weights = new Dictionary<Asset, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            weights[assets[i]] = w[i];
        }

        return weights;
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
                w[i] = Math.Max(0m, w[i]);
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

            // Check feasibility
            var feasible = true;
            for (var i = 0; i < n; i++)
            {
                if (w[i] < -1e-14m)
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
