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
/// Decorator that wraps any <see cref="IPortfolioConstructionModel"/> and penalizes turnover
/// by blending the inner model's target weights with previous weights.
///
/// Solves: minimize ||w - w_target||_2^2 + lambda * ||w - w_prev||_1
/// subject to: sum(w) = 1, minWeight ≤ w_i ≤ maxWeight
///
/// where w_target comes from the inner model and w_prev are the current portfolio weights.
///
/// Lambda controls the trade-off:
///   lambda = 0: pure inner model (no turnover penalty)
///   lambda → ∞: never trade (stay at previous weights)
///   Typical values: 0.01-0.10
///
/// The L1 penalty (||w - w_prev||_1) acts as a transaction cost proxy, encouraging the optimizer
/// to make fewer, larger trades rather than many small ones (sparsity in the delta vector).
/// </summary>
public sealed class TurnoverPenalizedConstruction : IPortfolioConstructionModel
{
    private readonly IPortfolioConstructionModel _inner;
    private readonly decimal _lambda;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;
    private readonly int _maxIterations;
    private readonly decimal _tolerance;
    private IReadOnlyDictionary<Asset, decimal>? _previousWeights;

    /// <summary>
    /// Initializes a new instance of the <see cref="TurnoverPenalizedConstruction"/> class.
    /// </summary>
    /// <param name="inner">The inner construction model to wrap.</param>
    /// <param name="lambda">Turnover penalty strength. Range [0, 1]. Typical: 0.01-0.10.</param>
    /// <param name="minWeight">Minimum weight per asset.</param>
    /// <param name="maxWeight">Maximum weight per asset.</param>
    /// <param name="maxIterations">Maximum solver iterations. Default 2000.</param>
    /// <param name="tolerance">Convergence tolerance. Default 1e-10.</param>
    public TurnoverPenalizedConstruction(
        IPortfolioConstructionModel inner,
        decimal lambda = 0.05m,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m,
        int maxIterations = 2000,
        decimal tolerance = 1e-10m)
    {
        Guard.AgainstNull(() => inner);

        if (lambda < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(lambda), lambda, "Lambda must be non-negative.");
        }

        _inner = inner;
        _lambda = lambda;
        _minWeight = minWeight;
        _maxWeight = maxWeight;
        _maxIterations = maxIterations;
        _tolerance = tolerance;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Tracks previous weights internally. On first call, delegates directly to the inner model.
    /// On subsequent calls, applies turnover penalty using internally stored previous weights.
    /// </remarks>
    public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns)
    {
        var result = ComputeTargetWeights(assets, returns, _previousWeights);
        _previousWeights = result;
        return result;
    }

    /// <summary>
    /// Core implementation: computes inner model's target and applies turnover penalty
    /// against the given current weights via proximal gradient iteration.
    /// </summary>
    private IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Asset, decimal>? currentWeights)
    {
        // Delegate to inner model first
        var target = _inner.ComputeTargetWeights(assets, returns);

        // If no current weights or lambda is zero, return inner model's target
        if (currentWeights is null || currentWeights.Count == 0 || _lambda == 0m)
        {
            return target;
        }

        var n = assets.Count;
        if (n == 0)
        {
            return target;
        }

        // Build arrays from dictionaries
        var wTarget = new decimal[n];
        var wPrev = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            wTarget[i] = target.TryGetValue(assets[i], out var tw) ? tw : 0m;
            wPrev[i] = currentWeights.TryGetValue(assets[i], out var pw) ? pw : 0m;
        }

        // Solve: minimize ||w - wTarget||_2^2 + lambda * ||w - wPrev||_1
        // Using iterative soft-thresholding (proximal gradient method)
        var w = new decimal[n];
        Array.Copy(wTarget, w, n); // Initialize at target

        for (var iter = 0; iter < _maxIterations; iter++)
        {
            var wOld = new decimal[n];
            Array.Copy(w, wOld, n);

            // Gradient of ||w - wTarget||_2^2 is 2*(w - wTarget)
            // Proximal step for L1 penalty: soft-threshold(w - step*grad, step*lambda)
            // With step size = 0.5 (for quadratic with Hessian = 2I)
            const decimal stepSize = 0.5m;

            for (var i = 0; i < n; i++)
            {
                // Gradient descent step
                var grad = 2m * (w[i] - wTarget[i]);
                var v = w[i] - stepSize * grad;

                // Proximal operator for lambda * |w_i - wPrev_i|
                // = soft-threshold centered at wPrev_i
                var diff = v - wPrev[i];
                var threshold = stepSize * _lambda;
                if (diff > threshold)
                {
                    w[i] = wPrev[i] + diff - threshold;
                }
                else if (diff < -threshold)
                {
                    w[i] = wPrev[i] + diff + threshold;
                }
                else
                {
                    w[i] = wPrev[i]; // No trade for this asset
                }
            }

            // Project onto constrained simplex
            ProjectOntoSimplex(w, _minWeight, _maxWeight);

            // Check convergence
            var maxDiff = 0m;
            for (var i = 0; i < n; i++)
            {
                maxDiff = Math.Max(maxDiff, Math.Abs(w[i] - wOld[i]));
            }

            if (maxDiff < _tolerance)
            {
                break;
            }
        }

        var result = new Dictionary<Asset, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            result[assets[i]] = w[i];
        }

        return result;
    }

    /// <summary>
    /// Projects weights onto the constrained simplex: minWeight ≤ w_i ≤ maxWeight, Σw_i = 1.
    /// </summary>
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
