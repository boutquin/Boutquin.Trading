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
using Domain.ValueObjects;

/// <summary>
/// Implements the Black-Litterman model for portfolio construction.
/// Combines market equilibrium returns with investor views to produce posterior expected returns,
/// then uses mean-variance optimization to compute weights.
/// </summary>
/// <remarks>
/// Reference: Black, F. &amp; Litterman, R. (1992). "Global portfolio optimization."
/// Financial Analysts Journal, 48(5), 28-43.
/// </remarks>
public sealed class BlackLittermanConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal[] _equilibriumWeights;
    private readonly decimal _riskAversionCoefficient;
    private readonly decimal _tau;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;

    // Views: P * μ = Q with uncertainty Ω
    private readonly decimal[,]? _pickMatrix; // P: K x N
    private readonly decimal[]? _viewReturns; // Q: K
    private readonly decimal[,]? _viewUncertainty; // Ω: K x K

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackLittermanConstruction"/> class.
    /// </summary>
    /// <param name="equilibriumWeights">Market capitalization weights (prior).</param>
    /// <param name="riskAversionCoefficient">Risk aversion coefficient (delta). Typical range 2-4.</param>
    /// <param name="tau">Scaling factor for uncertainty in the prior. Typical range 0.01-0.05.</param>
    /// <param name="pickMatrix">The pick matrix P (K x N) specifying which assets are in each view. Null if no views.</param>
    /// <param name="viewReturns">The expected returns for each view Q (K). Null if no views.</param>
    /// <param name="viewUncertainty">The uncertainty matrix Omega (K x K) for views. Null if no views.</param>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    public BlackLittermanConstruction(
        decimal[] equilibriumWeights,
        decimal riskAversionCoefficient = 2.5m,
        decimal tau = 0.05m,
        decimal[,]? pickMatrix = null,
        decimal[]? viewReturns = null,
        decimal[,]? viewUncertainty = null,
        ICovarianceEstimator? covarianceEstimator = null,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m)
    {
        Guard.AgainstNull(() => equilibriumWeights);

        _equilibriumWeights = (decimal[])equilibriumWeights.Clone();
        _riskAversionCoefficient = riskAversionCoefficient;
        _tau = tau;
        _pickMatrix = pickMatrix is not null ? (decimal[,])pickMatrix.Clone() : null;
        _viewReturns = viewReturns is not null ? (decimal[])viewReturns.Clone() : null;
        _viewUncertainty = viewUncertainty is not null ? (decimal[,])viewUncertainty.Clone() : null;
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

        if (_equilibriumWeights.Length != assets.Count)
        {
            throw new ArgumentException(
                $"Equilibrium weights length ({_equilibriumWeights.Length}) must match assets count ({assets.Count}).",
                nameof(assets));
        }

        var n = assets.Count;
        var sigma = _covarianceEstimator.Estimate(returns);

        // Step 1: Compute implied equilibrium returns: π = δ * Σ * w_eq
        var pi = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                pi[i] += _riskAversionCoefficient * sigma[i, j] * _equilibriumWeights[j];
            }
        }

        // Step 2: If no views, return equilibrium weights directly (CLAUDE.md convention).
        // Round-tripping through matrix inversion fails for singular covariance matrices
        // and introduces unnecessary numerical error.
        if (_pickMatrix is null || _viewReturns is null || _viewUncertainty is null)
        {
            var result = new Dictionary<Asset, decimal>(n);
            var assetList = assets is IList<Asset> list ? list : assets.ToList();
            for (var i = 0; i < n; i++)
            {
                result[assetList[i]] = _equilibriumWeights[i];
            }

            return result;
        }

        // Views are provided — compute Black-Litterman posterior
        decimal[] posteriorMu;
        {
            // Black-Litterman posterior: μ_BL = [(τΣ)^-1 + P'Ω^-1 P]^-1 * [(τΣ)^-1 π + P'Ω^-1 Q]
            // For simplicity and numerical stability, use the equivalent:
            // μ_BL = π + τΣP'(PτΣP' + Ω)^-1 (Q - Pπ)
            var k = _viewReturns.Length;

            // Compute τΣP' (N x K)
            var tauSigmaPt = new decimal[n, k];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    for (var l = 0; l < n; l++)
                    {
                        tauSigmaPt[i, j] += _tau * sigma[i, l] * _pickMatrix[j, l];
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
                        sum += _pickMatrix[i, l] * tauSigmaPt[l, j];
                    }

                    m[i, j] = sum + _viewUncertainty[i, j];
                }
            }

            // Invert M (K x K) — for K=1 this is trivial
            var mInv = InvertMatrix(m, k);

            // Compute (Q - Pπ) (K)
            var qMinusPpi = new decimal[k];
            for (var i = 0; i < k; i++)
            {
                var pPi = 0m;
                for (var j = 0; j < n; j++)
                {
                    pPi += _pickMatrix[i, j] * pi[j];
                }

                qMinusPpi[i] = _viewReturns[i] - pPi;
            }

            // Compute M^-1 (Q - Pπ) (K)
            var mInvQPpi = new decimal[k];
            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    mInvQPpi[i] += mInv[i, j] * qMinusPpi[j];
                }
            }

            // μ_BL = π + τΣP' * M^-1(Q - Pπ)
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

        // Step 3: Compute optimal weights from posterior returns
        // w* = (1/δ) * Σ^-1 * μ_BL
        // Try full matrix inversion; fall back to diagonal approximation if singular.
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
            // Singular covariance matrix — fall back to diagonal approximation:
            // w_i proportional to μ_i / σ_ii, then normalize.
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

        // Iterative clamping to [minWeight, maxWeight].
        // Auto-relax constraints when infeasible: with N assets, maxWeight must be >= 1/N
        // and minWeight must be <= 1/N, otherwise weights can't sum to 1.0.
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
    /// Inverts a small K x K matrix using Gauss-Jordan elimination.
    /// </summary>
    private const decimal SingularityEpsilon = 1e-20m;

    private static decimal[,] InvertMatrix(decimal[,] matrix, int size)
    {
        var augmented = new decimal[size, 2 * size];

        // Build augmented matrix [M | I]
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                augmented[i, j] = matrix[i, j];
            }

            augmented[i, size + i] = 1m;
        }

        // Forward elimination
        for (var col = 0; col < size; col++)
        {
            // Find pivot
            var maxRow = col;
            for (var row = col + 1; row < size; row++)
            {
                if (Math.Abs(augmented[row, col]) > Math.Abs(augmented[maxRow, col]))
                {
                    maxRow = row;
                }
            }

            // Swap rows
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

            // Scale pivot row
            var pivot = augmented[col, col];
            for (var j = 0; j < 2 * size; j++)
            {
                augmented[col, j] /= pivot;
            }

            // Eliminate column
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

        // Extract inverse
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
