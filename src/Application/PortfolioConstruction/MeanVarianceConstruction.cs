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
/// Computes mean-variance optimal (maximum Sharpe ratio) portfolio weights.
/// Maximizes utility U(w) = w'μ - (λ/2)*w'Σw subject to w_i ≥ 0, w_i ≤ maxWeight, Σw_i = 1.
/// Uses projected gradient ascent with iterative constraint projection.
/// </summary>
public sealed class MeanVarianceConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _maxWeight;
    private readonly decimal _riskAversion;
    private readonly int _maxIterations;
    private readonly decimal _tolerance;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeanVarianceConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="maxWeight">Maximum weight per asset (long-only constraint). Default 1.0 (no cap).</param>
    /// <param name="riskAversion">Risk aversion parameter λ. Higher values penalize variance more. Default 1.0.</param>
    /// <param name="maxIterations">Maximum gradient descent iterations. Default 5000.</param>
    /// <param name="tolerance">Convergence tolerance. Default 1e-12.</param>
    public MeanVarianceConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal maxWeight = 1.0m,
        decimal riskAversion = 1.0m,
        int maxIterations = 5000,
        decimal tolerance = 1e-12m)
    {
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _maxWeight = maxWeight;
        _riskAversion = riskAversion;
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
        var means = new decimal[n];

        for (var i = 0; i < n; i++)
        {
            means[i] = returns[i].Average();
        }

        // Start with equal weights
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m / n;
        }

        // Projected gradient ascent: maximize U(w) = w'μ - (λ/2)*w'Σw
        // Gradient: ∂U/∂w = μ - λΣw
        var learningRate = 1.0m;

        var converged = false;

        for (var iter = 0; iter < _maxIterations && !converged; iter++)
        {
            // Compute gradient: μ - λΣw
            var grad = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                var covW = 0m;
                for (var j = 0; j < n; j++)
                {
                    covW += cov[i, j] * w[j];
                }

                grad[i] = means[i] - _riskAversion * covW;
            }

            // Line search: try decreasing step sizes
            var stepped = false;
            var currentLr = learningRate;

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    candidate[i] = w[i] + currentLr * grad[i];
                }

                // Project onto feasible set
                ProjectOntoSimplex(candidate, _maxWeight);

                // Check if utility improved
                var newUtility = ComputeUtility(candidate, means, cov, _riskAversion);
                var oldUtility = ComputeUtility(w, means, cov, _riskAversion);

                if (newUtility > oldUtility)
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

    private static decimal ComputeUtility(decimal[] w, decimal[] means, decimal[,] cov, decimal lambda)
    {
        var n = w.Length;
        var portReturn = 0m;
        var portVariance = 0m;

        for (var i = 0; i < n; i++)
        {
            portReturn += w[i] * means[i];
            for (var j = 0; j < n; j++)
            {
                portVariance += w[i] * w[j] * cov[i, j];
            }
        }

        return portReturn - lambda / 2m * portVariance;
    }

    /// <summary>
    /// Projects weights onto the constrained simplex: w_i ≥ 0, w_i ≤ maxWeight, Σw_i = 1.
    /// Uses iterative clamping and renormalization.
    /// </summary>
    private static void ProjectOntoSimplex(decimal[] w, decimal maxWeight)
    {
        var n = w.Length;

        for (var round = 0; round < 50; round++)
        {
            // Clamp to [0, maxWeight]
            for (var i = 0; i < n; i++)
            {
                w[i] = Math.Max(0m, Math.Min(maxWeight, w[i]));
            }

            // Normalize
            var sum = w.Sum();
            if (sum <= 0m)
            {
                // Reset to equal weight
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

            // Check if all constraints satisfied
            var allSatisfied = true;
            for (var i = 0; i < n; i++)
            {
                if (w[i] > maxWeight + 1e-14m)
                {
                    allSatisfied = false;
                    break;
                }
            }

            if (allSatisfied)
            {
                return;
            }
        }
    }
}
