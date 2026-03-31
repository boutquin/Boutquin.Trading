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
/// Computes risk-parity weights where each asset contributes equally to total portfolio risk.
/// Uses an iterative Newton-like algorithm to find weights such that
/// MRC_i * w_i = MRC_j * w_j for all i, j.
/// </summary>
public sealed class RiskParityConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;
    private readonly int _maxIterations;
    private readonly decimal _tolerance;

    /// <summary>
    /// Initializes a new instance of the <see cref="RiskParityConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator to use. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    /// <param name="maxIterations">Maximum iterations for convergence. Default 100.</param>
    /// <param name="tolerance">Convergence tolerance. Default 1e-10.</param>
    public RiskParityConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m,
        int maxIterations = 100,
        decimal tolerance = 1e-10m)
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
        var cov = _covarianceEstimator.Estimate(returns);

        // Start with equal weights
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m / n;
        }

        // Iterative algorithm: at each step, set w_i proportional to 1 / (Σ_j cov[i,j] * w_j)
        // then normalize so weights sum to 1
        for (var iter = 0; iter < _maxIterations; iter++)
        {
            var newW = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                // Marginal risk contribution: (Cov * w)_i
                var mrc = 0m;
                for (var j = 0; j < n; j++)
                {
                    mrc += cov[i, j] * w[j];
                }

                if (mrc <= 0m)
                {
                    throw new Boutquin.Trading.Domain.Exceptions.CalculationException(
                        $"Risk parity is undefined when marginal risk contribution is <= 0 (asset has negative risk contribution, typical for hedging assets). MRC = {mrc}.");
                }

                newW[i] = 1m / mrc;
            }

            // Normalize then clamp to [minWeight, maxWeight]
            var sumW = newW.Sum();
            for (var i = 0; i < n; i++)
            {
                newW[i] /= sumW;
            }

            // Apply weight constraints via iterative clamping.
            // Auto-relax constraints when infeasible: with N assets, maxWeight must be >= 1/N
            // and minWeight must be <= 1/N, otherwise weights can't sum to 1.0.
            var effectiveMax = Math.Max(_maxWeight, 1m / n);
            var effectiveMin = Math.Min(_minWeight, 1m / n);
            for (var clampRound = 0; clampRound < 50; clampRound++)
            {
                for (var i = 0; i < n; i++)
                {
                    newW[i] = Math.Max(effectiveMin, Math.Min(effectiveMax, newW[i]));
                }

                var clampSum = newW.Sum();
                if (clampSum <= 0m)
                {
                    break;
                }

                for (var i = 0; i < n; i++)
                {
                    newW[i] /= clampSum;
                }

                var feasible = true;
                for (var i = 0; i < n; i++)
                {
                    if (newW[i] < effectiveMin - 1e-14m || newW[i] > effectiveMax + 1e-14m)
                    {
                        feasible = false;
                        break;
                    }
                }

                if (feasible)
                {
                    break;
                }
            }

            // Check convergence
            var maxDiff = 0m;
            for (var i = 0; i < n; i++)
            {
                maxDiff = Math.Max(maxDiff, Math.Abs(newW[i] - w[i]));
            }

            w = newW;

            if (maxDiff < _tolerance)
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
}
