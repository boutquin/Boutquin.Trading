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
using Domain.Interfaces;
using Domain.ValueObjects;

/// <summary>
/// A DynamicUniverse-compatible Black-Litterman implementation that stores views by asset name
/// and builds all numeric matrices (equilibrium weights, pick matrix, view returns, omega)
/// at each rebalance from the currently-eligible assets.
/// </summary>
/// <remarks>
/// Unlike <see cref="BlackLittermanConstruction"/>, this class does not require a fixed-size
/// equilibrium weights array at construction time. Equilibrium weights are computed as 1/N
/// at each call to <see cref="ComputeTargetWeights"/>. Views referencing assets not in the
/// current universe are silently filtered out.
/// </remarks>
public sealed class DynamicBlackLittermanConstruction : IPortfolioConstructionModel
{
    private readonly IReadOnlyList<BlackLittermanViewSpec> _viewSpecs;
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _riskAversionCoefficient;
    private readonly decimal _tau;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicBlackLittermanConstruction"/> class.
    /// </summary>
    /// <param name="viewSpecs">Investor views expressed by asset name.</param>
    /// <param name="riskAversionCoefficient">Risk aversion coefficient (delta). Typical range 2-4.</param>
    /// <param name="tau">Scaling factor for uncertainty in the prior. Typical range 0.01-0.05.</param>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    public DynamicBlackLittermanConstruction(
        IReadOnlyList<BlackLittermanViewSpec> viewSpecs,
        decimal riskAversionCoefficient = 2.5m,
        decimal tau = 0.05m,
        ICovarianceEstimator? covarianceEstimator = null,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m)
    {
        Guard.AgainstNull(() => viewSpecs);

        _viewSpecs = viewSpecs;
        _riskAversionCoefficient = riskAversionCoefficient;
        _tau = tau;
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _minWeight = minWeight;
        _maxWeight = maxWeight;
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

        // Build asset name → index mapping for the current universe
        var tickerIndex = new Dictionary<string, int>(n, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < n; i++)
        {
            tickerIndex[assets[i].Ticker] = i;
        }

        // Filter views to only those referencing currently-eligible assets
        var filteredViews = FilterViews(tickerIndex);

        // Equilibrium weights: 1/N (equal-weight prior)
        var equilibriumWeights = new decimal[n];
        var equalWeight = 1m / n;
        for (var i = 0; i < n; i++)
        {
            equilibriumWeights[i] = equalWeight;
        }

        // Estimate covariance
        var sigma = _covarianceEstimator.Estimate(returns);

        // Step 1: Implied equilibrium returns: π = δ * Σ * w_eq
        var pi = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                pi[i] += _riskAversionCoefficient * sigma[i, j] * equilibriumWeights[j];
            }
        }

        // Step 2: Posterior returns
        decimal[] posteriorMu;

        if (filteredViews.Count == 0)
        {
            posteriorMu = pi;
        }
        else
        {
            var k = filteredViews.Count;

            // Build pick matrix P (K x N) and view returns Q (K)
            var pickMatrix = new decimal[k, n];
            var viewReturns = new decimal[k];

            for (var v = 0; v < k; v++)
            {
                var view = filteredViews[v];
                viewReturns[v] = view.ExpectedReturn;

                if (view.Type == BlackLittermanViewType.Absolute)
                {
                    pickMatrix[v, tickerIndex[view.Asset!]] = 1m;
                }
                else // Relative
                {
                    pickMatrix[v, tickerIndex[view.LongAsset!]] = 1m;
                    pickMatrix[v, tickerIndex[view.ShortAsset!]] = -1m;
                }
            }

            // Omega Ω (K x K) — Idzorek (2005) formulation:
            // Ω[v,v] = (1/C - 1) × P[v,:] × τΣ × P[v,:]'
            // This scales view uncertainty by the view portfolio's variance,
            // so confidence maps directly to tilt percentage: C=0.6 → 60% tilt
            // toward the view. Without variance scaling, high-vol assets
            // (equities) barely respond to views — the gain factor collapses.
            var omega = new decimal[k, k];
            for (var v = 0; v < k; v++)
            {
                // Compute P[v,:] × τΣ × P[v,:]' (scalar — view portfolio variance × τ)
                var pTauSigmaPt = 0m;
                for (var i = 0; i < n; i++)
                {
                    for (var j = 0; j < n; j++)
                    {
                        pTauSigmaPt += pickMatrix[v, i] * _tau * sigma[i, j] * pickMatrix[v, j];
                    }
                }

                var uncertaintyScale = (1m / filteredViews[v].Confidence - 1m);
                omega[v, v] = Math.Max(uncertaintyScale * pTauSigmaPt, 1e-10m);
            }

            // BL posterior: μ_BL = π + τΣP'(PτΣP' + Ω)⁻¹(Q - Pπ)

            // Compute τΣP' (N x K)
            var tauSigmaPt = new decimal[n, k];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    for (var l = 0; l < n; l++)
                    {
                        tauSigmaPt[i, j] += _tau * sigma[i, l] * pickMatrix[j, l];
                    }
                }
            }

            // Compute PτΣP' + Ω (K x K)
            var m = new decimal[k, k];
            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    var sum = 0m;
                    for (var l = 0; l < n; l++)
                    {
                        sum += pickMatrix[i, l] * tauSigmaPt[l, j];
                    }

                    m[i, j] = sum + omega[i, j];
                }
            }

            // Invert M
            var mInv = InvertMatrix(m, k);

            // Compute (Q - Pπ)
            var qMinusPpi = new decimal[k];
            for (var i = 0; i < k; i++)
            {
                var pPi = 0m;
                for (var j = 0; j < n; j++)
                {
                    pPi += pickMatrix[i, j] * pi[j];
                }

                qMinusPpi[i] = viewReturns[i] - pPi;
            }

            // Compute M⁻¹(Q - Pπ)
            var mInvQPpi = new decimal[k];
            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    mInvQPpi[i] += mInv[i, j] * qMinusPpi[j];
                }
            }

            // μ_BL = π + τΣP' * M⁻¹(Q - Pπ)
            posteriorMu = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                posteriorMu[i] = pi[i];
                for (var j = 0; j < k; j++)
                {
                    posteriorMu[i] += tauSigmaPt[i, j] * mInvQPpi[j];
                }
            }
        }

        // Step 3: Optimal weights from posterior: w* = (1/δ) * Σ⁻¹ * μ_BL
        var rawWeights = new decimal[n];

        try
        {
            var sigmaInv = InvertMatrix(sigma, n);

            for (var i = 0; i < n; i++)
            {
                var sum = 0m;
                for (var j = 0; j < n; j++)
                {
                    sum += sigmaInv[i, j] * posteriorMu[j];
                }

                rawWeights[i] = sum / _riskAversionCoefficient;
            }
        }
        catch (CalculationException)
        {
            // Singular covariance — diagonal fallback
            for (var i = 0; i < n; i++)
            {
                var variance = sigma[i, i];
                rawWeights[i] = variance > 0m ? posteriorMu[i] / (_riskAversionCoefficient * variance) : 0m;
            }
        }

        // Normalize then apply weight constraints
        var sumRaw = 0m;
        for (var i = 0; i < n; i++)
        {
            rawWeights[i] = Math.Max(0m, rawWeights[i]);
            sumRaw += rawWeights[i];
        }

        for (var i = 0; i < n; i++)
        {
            rawWeights[i] = sumRaw > 0m ? rawWeights[i] / sumRaw : 1m / n;
        }

        // Iterative clamping to [minWeight, maxWeight]
        var effectiveMax = Math.Max(_maxWeight, 1m / n);
        var effectiveMin = Math.Min(_minWeight, 1m / n);
        for (var round = 0; round < 50; round++)
        {
            for (var i = 0; i < n; i++)
            {
                rawWeights[i] = Math.Max(effectiveMin, Math.Min(effectiveMax, rawWeights[i]));
            }

            var clampSum = rawWeights.Sum();
            if (clampSum <= 0m)
            {
                break;
            }

            for (var i = 0; i < n; i++)
            {
                rawWeights[i] /= clampSum;
            }

            var feasible = true;
            for (var i = 0; i < n; i++)
            {
                if (rawWeights[i] < effectiveMin - 1e-14m || rawWeights[i] > effectiveMax + 1e-14m)
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

        var weights = new Dictionary<Asset, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            weights[assets[i]] = rawWeights[i];
        }

        return weights;
    }

    /// <summary>
    /// Filters stored view specs to only those referencing currently-eligible assets.
    /// </summary>
    private List<BlackLittermanViewSpec> FilterViews(Dictionary<string, int> tickerIndex)
    {
        var filtered = new List<BlackLittermanViewSpec>(_viewSpecs.Count);

        foreach (var view in _viewSpecs)
        {
            if (view.Type == BlackLittermanViewType.Absolute)
            {
                if (view.Asset is not null && tickerIndex.ContainsKey(view.Asset))
                {
                    filtered.Add(view);
                }
            }
            else // Relative
            {
                if (view.LongAsset is not null && view.ShortAsset is not null &&
                    tickerIndex.ContainsKey(view.LongAsset) && tickerIndex.ContainsKey(view.ShortAsset))
                {
                    filtered.Add(view);
                }
            }
        }

        return filtered;
    }

    /// <summary>
    /// Inverts a small K x K matrix using Gauss-Jordan elimination.
    /// </summary>
    private const decimal SingularityEpsilon = 1e-20m;

    private static decimal[,] InvertMatrix(decimal[,] matrix, int size)
    {
        var augmented = new decimal[size, 2 * size];

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                augmented[i, j] = matrix[i, j];
            }

            augmented[i, size + i] = 1m;
        }

        for (var col = 0; col < size; col++)
        {
            var maxRow = col;
            for (var row = col + 1; row < size; row++)
            {
                if (Math.Abs(augmented[row, col]) > Math.Abs(augmented[maxRow, col]))
                {
                    maxRow = row;
                }
            }

            if (maxRow != col)
            {
                for (var j = 0; j < 2 * size; j++)
                {
                    (augmented[col, j], augmented[maxRow, j]) = (augmented[maxRow, j], augmented[col, j]);
                }
            }

            if (Math.Abs(augmented[col, col]) < SingularityEpsilon)
            {
                throw new CalculationException("Matrix is singular and cannot be inverted.");
            }

            var pivot = augmented[col, col];
            for (var j = 0; j < 2 * size; j++)
            {
                augmented[col, j] /= pivot;
            }

            for (var row = 0; row < size; row++)
            {
                if (row == col)
                {
                    continue;
                }

                var factor = augmented[row, col];
                for (var j = 0; j < 2 * size; j++)
                {
                    augmented[row, j] -= factor * augmented[col, j];
                }
            }
        }

        var inverse = new decimal[size, size];
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                inverse[i, j] = augmented[i, size + j];
            }
        }

        return inverse;
    }
}
