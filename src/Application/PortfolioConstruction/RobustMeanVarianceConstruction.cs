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

using Domain.ValueObjects;

/// <summary>
/// Robust mean-variance portfolio construction that maximizes the worst-case Sharpe ratio
/// across a set of covariance scenarios (minimax optimization).
///
/// Solves: max_w min_s { w'μ / sqrt(w' Σ_s w) } subject to sum(w) = 1, minW ≤ w_i ≤ maxW
///
/// Uses alternating optimization:
/// 1. Fix scenario → optimize weights (projected gradient ascent on Sharpe)
/// 2. Fix weights → find worst scenario (minimum Sharpe across scenarios)
/// 3. Repeat until convergence
///
/// When called without scenarios (base interface), uses a single sample covariance matrix.
/// </summary>
public sealed class RobustMeanVarianceConstruction : IRobustConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;
    private readonly decimal _riskAversion;
    private readonly int _maxIterations;
    private readonly int _maxAlternatingRounds;
    private readonly decimal _tolerance;

    /// <summary>
    /// Initializes a new instance of the <see cref="RobustMeanVarianceConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">Estimator for fallback single-scenario mode.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0.</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0.</param>
    /// <param name="riskAversion">Risk aversion parameter. Default 1.0.</param>
    /// <param name="maxIterations">Max gradient iterations per round. Default 3000.</param>
    /// <param name="maxAlternatingRounds">Max alternating optimization rounds. Default 20.</param>
    /// <param name="tolerance">Convergence tolerance. Default 1e-10.</param>
    public RobustMeanVarianceConstruction(
        ICovarianceEstimator covarianceEstimator,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m,
        decimal riskAversion = 1.0m,
        int maxIterations = 3000,
        int maxAlternatingRounds = 20,
        decimal tolerance = 1e-10m)
    {
        Guard.AgainstNull(() => covarianceEstimator);

        _covarianceEstimator = covarianceEstimator;
        _minWeight = minWeight;
        _maxWeight = maxWeight;
        _riskAversion = riskAversion;
        _maxIterations = maxIterations;
        _maxAlternatingRounds = maxAlternatingRounds;
        _tolerance = tolerance;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Single-scenario fallback: estimates covariance from returns and optimizes mean-variance.
    /// </remarks>
    public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns)
    {
        Guard.AgainstNull(() => assets);

        if (assets.Count == 0)
        {
            return new Dictionary<Asset, decimal>();
        }

        var cov = _covarianceEstimator.Estimate(returns);
        return ComputeTargetWeights(assets, returns, new[] { cov });
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns,
        IReadOnlyList<decimal[,]> covarianceScenarios)
    {
        Guard.AgainstNull(() => assets);
        Guard.AgainstNull(() => covarianceScenarios);

        var n = assets.Count;
        if (n == 0)
        {
            return new Dictionary<Asset, decimal>();
        }

        if (covarianceScenarios.Count == 0)
        {
            throw new ArgumentException("At least one covariance scenario is required.", nameof(covarianceScenarios));
        }

        if (returns is null || returns.Length != n)
        {
            throw new ArgumentException("Returns array must have one series per asset.", nameof(returns));
        }

        // Compute mean returns
        var means = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            means[i] = returns[i].Average();
        }

        // Initialize with equal weights
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m / n;
        }

        // If only one scenario, just do standard mean-variance
        if (covarianceScenarios.Count == 1)
        {
            return OptimizeMeanVariance(assets, means, covarianceScenarios[0], w, n);
        }

        // Alternating optimization: max_w min_s utility(w, Sigma_s)
        var bestWorstUtility = decimal.MinValue;
        var bestW = new decimal[n];
        Array.Copy(w, bestW, n);

        for (var round = 0; round < _maxAlternatingRounds; round++)
        {
            // Step 1: Fix weights → find worst-case scenario
            var worstScenarioIdx = FindWorstScenario(w, means, covarianceScenarios, n);

            // Step 2: Fix scenario → optimize weights
            var wNew = new decimal[n];
            Array.Copy(w, wNew, n);
            OptimizeForScenario(wNew, means, covarianceScenarios[worstScenarioIdx], n);

            // Compute worst-case utility for new weights
            var newWorstIdx = FindWorstScenario(wNew, means, covarianceScenarios, n);
            var newWorstUtility = ComputeUtility(wNew, means, covarianceScenarios[newWorstIdx], n);

            if (newWorstUtility > bestWorstUtility)
            {
                bestWorstUtility = newWorstUtility;
                Array.Copy(wNew, bestW, n);
            }

            // Check convergence
            var maxDiff = 0m;
            for (var i = 0; i < n; i++)
            {
                maxDiff = Math.Max(maxDiff, Math.Abs(wNew[i] - w[i]));
            }

            Array.Copy(wNew, w, n);

            if (maxDiff < _tolerance)
            {
                break;
            }
        }

        var result = new Dictionary<Asset, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            result[assets[i]] = bestW[i];
        }

        return result;
    }

    private IReadOnlyDictionary<Asset, decimal> OptimizeMeanVariance(
        IReadOnlyList<Asset> assets, decimal[] means, decimal[,] cov, decimal[] w, int n)
    {
        OptimizeForScenario(w, means, cov, n);

        var result = new Dictionary<Asset, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            result[assets[i]] = w[i];
        }

        return result;
    }

    /// <summary>
    /// Optimizes weights for a single covariance scenario using projected gradient ascent
    /// on the mean-variance utility: U(w) = w'μ - (λ/2) * w'Σw.
    /// </summary>
    private void OptimizeForScenario(decimal[] w, decimal[] means, decimal[,] cov, int n)
    {
        var learningRate = 0.1m;

        for (var iter = 0; iter < _maxIterations; iter++)
        {
            // Gradient of U(w) = μ - λ * Σw
            var grad = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                grad[i] = means[i];
                for (var j = 0; j < n; j++)
                {
                    grad[i] -= _riskAversion * cov[i, j] * w[j];
                }
            }

            // Line search
            var stepped = false;
            var currentLr = learningRate;
            var currentUtility = ComputeUtility(w, means, cov, n);

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    candidate[i] = w[i] + currentLr * grad[i];
                }

                ProjectOntoSimplex(candidate, _minWeight, _maxWeight);

                var newUtility = ComputeUtility(candidate, means, cov, n);
                if (newUtility > currentUtility)
                {
                    var maxDiff = 0m;
                    for (var i = 0; i < n; i++)
                    {
                        maxDiff = Math.Max(maxDiff, Math.Abs(candidate[i] - w[i]));
                    }

                    Array.Copy(candidate, w, n);
                    stepped = true;

                    if (maxDiff < _tolerance)
                    {
                        return;
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
    }

    /// <summary>
    /// Finds the scenario index that produces the lowest utility for the given weights.
    /// </summary>
    private int FindWorstScenario(
        decimal[] w, decimal[] means, IReadOnlyList<decimal[,]> scenarios, int n)
    {
        var worstIdx = 0;
        var worstUtility = ComputeUtility(w, means, scenarios[0], n);

        for (var s = 1; s < scenarios.Count; s++)
        {
            var utility = ComputeUtility(w, means, scenarios[s], n);
            if (utility < worstUtility)
            {
                worstUtility = utility;
                worstIdx = s;
            }
        }

        return worstIdx;
    }

    /// <summary>
    /// Computes mean-variance utility: U(w) = w'μ - (λ/2) * w'Σw.
    /// </summary>
    private decimal ComputeUtility(decimal[] w, decimal[] means, decimal[,] cov, int n)
    {
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

        return portReturn - _riskAversion * 0.5m * portVariance;
    }

    private static void ProjectOntoSimplex(decimal[] w, decimal minWeight, decimal maxWeight)
    {
        var n = w.Length;
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

            var allSatisfied = true;
            for (var i = 0; i < n; i++)
            {
                if (w[i] < minWeight - 1e-14m || w[i] > maxWeight + 1e-14m)
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
