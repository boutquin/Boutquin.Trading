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
/// Computes mean-variance optimal (maximum Sharpe ratio) portfolio weights.
/// Maximizes utility U(w) = w'μ - (λ/2)*w'Σw subject to minWeight ≤ w_i ≤ maxWeight, Σw_i = 1.
/// Uses projected gradient ascent with iterative constraint projection.
/// </summary>
public sealed class MeanVarianceConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;
    private readonly decimal _riskAversion;
    private readonly int _maxIterations;
    private readonly decimal _tolerance;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeanVarianceConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    /// <param name="riskAversion">Risk aversion parameter λ. Higher values penalize variance more. Default 1.0.</param>
    /// <param name="maxIterations">Maximum gradient descent iterations. Default 5000.</param>
    /// <param name="tolerance">Convergence tolerance. Default 1e-12.</param>
    public MeanVarianceConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m,
        decimal riskAversion = 1.0m,
        int maxIterations = 5000,
        decimal tolerance = 1e-12m)
    {
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _minWeight = minWeight;
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

        decimal[] w;
        try
        {
            w = CholeskyQpSolver.SolveMeanVarianceQP(cov, means, n, _riskAversion, _minWeight, _maxWeight);

            // Verify bounds are satisfied (active-set cycling can produce slight violations)
            var effectiveMin = Math.Min(_minWeight, 1m / n);
            var effectiveMax = Math.Max(_maxWeight, 1m / n);
            var boundsSatisfied = true;
            for (var i = 0; i < n; i++)
            {
                if (w[i] < effectiveMin - 1e-10m || w[i] > effectiveMax + 1e-10m)
                {
                    boundsSatisfied = false;
                    break;
                }
            }

            if (!boundsSatisfied)
            {
                w = SolveGradientAscent(cov, means, n);
            }
        }
        catch (CalculationException)
        {
            // Fallback to gradient ascent for non-positive-definite matrices
            w = SolveGradientAscent(cov, means, n);
        }

        var weights = new Dictionary<Asset, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            weights[assets[i]] = w[i];
        }

        return weights;
    }

    private decimal[] SolveGradientAscent(decimal[,] cov, decimal[] means, int n)
    {
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m / n;
        }

        for (var iter = 0; iter < _maxIterations; iter++)
        {
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

            var stepped = false;
            var currentLr = 1.0m;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    candidate[i] = w[i] + currentLr * grad[i];
                }

                ProjectOntoSimplex(candidate, _minWeight, _maxWeight);
                if (ComputeUtility(candidate, means, cov, _riskAversion) > ComputeUtility(w, means, cov, _riskAversion))
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
