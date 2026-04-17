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

/// <summary>
/// PCA-Constrained construction model decorator.
///
/// Projects N-dimensional returns onto the K-dimensional principal component (signal) subspace
/// before delegating to an inner <see cref="IPortfolioConstructionModel"/>. The inner model
/// operates in the reduced PC space, and the resulting weights are mapped back to asset space.
///
/// K is determined automatically via the Marcenko-Pastur upper bound (signal eigenvalues),
/// or capped by an optional <c>maxComponents</c> parameter.
///
/// This removes noise dimensions from the optimization, producing more stable weights
/// and lower turnover — especially beneficial for optimization-based models (MDP, MVO, etc.)
/// on noisy covariance matrices.
///
/// Not compatible with hierarchical models (HRP, HERC) which do not use covariance-based optimization.
/// </summary>
public sealed class PcaConstrainedConstruction : IPortfolioConstructionModel
{
    private static readonly HashSet<Type> s_hierarchicalTypes =
    [
        typeof(HierarchicalRiskParityConstruction),
        typeof(ReturnTiltedHrpConstruction)
    ];

    private readonly IPortfolioConstructionModel _innerModel;
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly int? _maxComponents;

    /// <summary>
    /// Initializes a new instance of the <see cref="PcaConstrainedConstruction"/> class.
    /// </summary>
    /// <param name="innerModel">The inner construction model to wrap. Must not be a hierarchical model.</param>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="maxComponents">
    /// Optional cap on number of PCs retained. When null, all signal PCs (above MP bound) are retained.
    /// Set to 0 to force equal-weight fallback (useful for testing).
    /// </param>
    public PcaConstrainedConstruction(
        IPortfolioConstructionModel innerModel,
        ICovarianceEstimator? covarianceEstimator = null,
        int? maxComponents = null)
    {
        Guard.AgainstNull(() => innerModel);

        if (s_hierarchicalTypes.Contains(innerModel.GetType()))
        {
            throw new ArgumentException(
                "PCA constraint is not compatible with hierarchical models (HRP, HERC) which do not use covariance-based optimization.",
                nameof(innerModel));
        }

        _innerModel = innerModel;
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _maxComponents = maxComponents;
    }

    /// <inheritdoc />
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

        // Step 1: Estimate covariance and eigendecompose
        var cov = _covarianceEstimator.Estimate(returns);
        var (eigenvalues, eigenvectors) = EigenDecompose(cov, n);

        // Step 2: Determine K (number of signal PCs)
        var threshold = ComputeThreshold(returns, n);
        var k = 0;
        for (var i = 0; i < n; i++)
        {
            if (eigenvalues[i] > threshold)
            {
                k++;
            }
        }

        // Apply maxComponents cap
        if (_maxComponents.HasValue)
        {
            k = Math.Min(k, _maxComponents.Value);
        }

        // Step 3: K=0 → equal-weight fallback
        if (k <= 0)
        {
            return EqualWeightFallback(assets);
        }

        // Step 4: K=N → passthrough (no dimensionality reduction needed)
        if (k >= n)
        {
            return _innerModel.ComputeTargetWeights(assets, returns);
        }

        // Step 5: Project N-dimensional returns onto K-dimensional PC space
        // projected[pc][t] = Σ_j eigenvectors[j, pc] * returns[j][t]
        var t = returns[0].Length;
        var syntheticAssets = new List<Symbol>(k);
        var projectedReturns = new decimal[k][];

        for (var pc = 0; pc < k; pc++)
        {
            syntheticAssets.Add(new Symbol($"PC{pc}"));
            projectedReturns[pc] = new decimal[t];
            for (var ti = 0; ti < t; ti++)
            {
                var val = 0m;
                for (var j = 0; j < n; j++)
                {
                    val += eigenvectors[j, pc] * returns[j][ti];
                }

                projectedReturns[pc][ti] = val;
            }
        }

        // Step 6: Delegate to inner model in K-dimensional space
        var pcWeights = _innerModel.ComputeTargetWeights(syntheticAssets, projectedReturns);

        // Step 7: Map K-dimensional weights back to N-dimensional asset weights
        // w_asset[j] = Σ_pc pcWeight[pc] * eigenvectors[j, pc]
        var assetWeights = new decimal[n];
        for (var pc = 0; pc < k; pc++)
        {
            var pcWeight = pcWeights[syntheticAssets[pc]];
            for (var j = 0; j < n; j++)
            {
                assetWeights[j] += pcWeight * eigenvectors[j, pc];
            }
        }

        // Step 8: Clamp negatives to zero (AC-B7)
        for (var j = 0; j < n; j++)
        {
            if (assetWeights[j] < 0m)
            {
                assetWeights[j] = 0m;
            }
        }

        // Step 9: Normalize to sum to 1 (AC-B6)
        var sum = assetWeights.Sum();
        if (sum <= 0m)
        {
            return EqualWeightFallback(assets);
        }

        for (var j = 0; j < n; j++)
        {
            assetWeights[j] /= sum;
        }

        var weights = new Dictionary<Symbol, decimal>(n);
        for (var j = 0; j < n; j++)
        {
            weights[assets[j]] = assetWeights[j];
        }

        return weights;
    }

    private static decimal ComputeThreshold(decimal[][] returns, int n)
    {
        var t = returns[0].Length;
        var q = (decimal)t / n;

        if (q < 1m)
        {
            // MP bound undefined when q < 1; use Kaiser criterion
            return 1.0m;
        }

        // Marcenko-Pastur upper bound: λ₊ = (1 + 1/√q)²
        var sqrtQ = DecimalSqrt(q);
        if (sqrtQ == 0m)
        {
            return decimal.MaxValue;
        }

        var bound = 1m + 1m / sqrtQ;
        return bound * bound;
    }

    private static IReadOnlyDictionary<Symbol, decimal> EqualWeightFallback(IReadOnlyList<Symbol> assets)
    {
        var weight = 1m / assets.Count;
        var weights = new Dictionary<Symbol, decimal>(assets.Count);
        foreach (var asset in assets)
        {
            weights[asset] = weight;
        }

        return weights;
    }

    private static (decimal[] Eigenvalues, decimal[,] Eigenvectors) EigenDecompose(
        decimal[,] matrix, int n)
    {
        var a = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                a[i, j] = (double)matrix[i, j];
            }
        }

        var v = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            v[i, i] = 1.0;
        }

        const int maxSweeps = 100;
        const double threshold = 1e-15;

        for (var sweep = 0; sweep < maxSweeps; sweep++)
        {
            var offDiagSum = 0.0;
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    offDiagSum += a[i, j] * a[i, j];
                }
            }

            if (offDiagSum < threshold)
            {
                break;
            }

            for (var p = 0; p < n - 1; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < threshold)
                    {
                        continue;
                    }

                    var diff = a[q, q] - a[p, p];
                    double t;
                    if (Math.Abs(diff) < threshold)
                    {
                        t = 1.0;
                    }
                    else
                    {
                        var phi = diff / (2.0 * a[p, q]);
                        t = Math.Sign(phi) / (Math.Abs(phi) + Math.Sqrt(phi * phi + 1.0));
                    }

                    var c = 1.0 / Math.Sqrt(t * t + 1.0);
                    var s = t * c;
                    var tau = s / (1.0 + c);

                    var apq = a[p, q];
                    a[p, q] = 0;
                    a[p, p] -= t * apq;
                    a[q, q] += t * apq;

                    for (var r = 0; r < p; r++)
                    {
                        Rotate(a, r, p, r, q, s, tau);
                    }

                    for (var r = p + 1; r < q; r++)
                    {
                        Rotate(a, p, r, r, q, s, tau);
                    }

                    for (var r = q + 1; r < n; r++)
                    {
                        Rotate(a, p, r, q, r, s, tau);
                    }

                    for (var r = 0; r < n; r++)
                    {
                        var vRp = v[r, p];
                        var vRq = v[r, q];
                        v[r, p] = vRp - s * (vRq + tau * vRp);
                        v[r, q] = vRq + s * (vRp - tau * vRq);
                    }
                }
            }
        }

        var eigenvalues = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            eigenvalues[i] = (decimal)a[i, i];
        }

        var indices = Enumerable.Range(0, n).OrderByDescending(i => eigenvalues[i]).ToArray();
        var sortedEigenvalues = new decimal[n];
        var sortedEigenvectors = new decimal[n, n];
        for (var k = 0; k < n; k++)
        {
            sortedEigenvalues[k] = eigenvalues[indices[k]];
            for (var r = 0; r < n; r++)
            {
                sortedEigenvectors[r, k] = (decimal)v[r, indices[k]];
            }
        }

        return (sortedEigenvalues, sortedEigenvectors);
    }

    private static void Rotate(double[,] a, int i1, int j1, int i2, int j2, double s, double tau)
    {
        var g1 = a[i1, j1];
        var g2 = a[i2, j2];
        a[i1, j1] = g1 - s * (g2 + tau * g1);
        a[i2, j2] = g2 + s * (g1 - tau * g2);
    }

    private static decimal DecimalSqrt(decimal value)
    {
        if (value <= 0m)
        {
            return 0m;
        }

        var guess = (decimal)Math.Sqrt((double)value);
        if (guess == 0m)
        {
            guess = 1m;
        }

        for (var i = 0; i < 30; i++)
        {
            var next = (guess + value / guess) * 0.5m;
            if (Math.Abs(next - guess) < 1e-28m)
            {
                break;
            }

            guess = next;
        }

        return guess;
    }
}
