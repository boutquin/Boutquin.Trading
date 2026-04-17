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
/// Principal Component Risk Parity — equalizes risk across statistical factors (principal components)
/// rather than individual assets.
///
/// Algorithm:
/// 1. Estimate the covariance matrix via the injected <see cref="ICovarianceEstimator"/>.
/// 2. Eigendecompose the covariance matrix (Jacobi iteration).
/// 3. Filter signal PCs using the Marcenko-Pastur upper bound (or Kaiser criterion when q &lt; 1).
/// 4. Allocate inverse-risk (1/√λ) across signal PCs.
/// 5. Map PC-space weights back to asset-space weights via eigenvectors.
/// 6. Clamp negatives to zero and normalize to sum to 1.
///
/// Falls back to equal-weight when all eigenvalues are below the signal threshold.
///
/// Reference: Meucci, A. (2009). "Managing Diversification." Risk Magazine.
/// </summary>
public sealed class PrincipalComponentRiskParityConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal? _signalThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrincipalComponentRiskParityConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="signalThreshold">
    /// Eigenvalue cutoff for PC inclusion. PCs with eigenvalues at or below this threshold are excluded.
    /// Default: auto-computed from Marcenko-Pastur upper bound λ₊ = (1 + 1/√q)² where q = T/N.
    /// When q &lt; 1 (more assets than observations), falls back to Kaiser criterion (eigenvalue &gt; 1.0).
    /// </param>
    public PrincipalComponentRiskParityConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal? signalThreshold = null)
    {
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _signalThreshold = signalThreshold;
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

        // Step 1: Estimate covariance matrix
        var cov = _covarianceEstimator.Estimate(returns);

        // Step 2: Eigendecompose (descending eigenvalues)
        var (eigenvalues, eigenvectors) = EigenDecompose(cov, n);

        // Step 3: Compute signal threshold
        var threshold = _signalThreshold ?? ComputeDefaultThreshold(returns, n);

        // Step 4: Identify signal PCs (eigenvalue > threshold)
        var signalIndices = new List<int>();
        for (var k = 0; k < n; k++)
        {
            if (eigenvalues[k] > threshold)
            {
                signalIndices.Add(k);
            }
        }

        // Step 5: Degenerate case — all noise → equal-weight fallback
        if (signalIndices.Count == 0)
        {
            return EqualWeightFallback(assets);
        }

        // Step 6: Allocate inverse-risk (1/√λ) across signal PCs
        var pcWeights = new decimal[signalIndices.Count];
        var totalInvRisk = 0m;
        for (var i = 0; i < signalIndices.Count; i++)
        {
            var lambda = eigenvalues[signalIndices[i]];
            if (lambda > 0m)
            {
                var invRisk = 1m / DecimalSqrt(lambda);
                pcWeights[i] = invRisk;
                totalInvRisk += invRisk;
            }
        }

        if (totalInvRisk <= 0m)
        {
            return EqualWeightFallback(assets);
        }

        // Normalize PC weights
        for (var i = 0; i < pcWeights.Length; i++)
        {
            pcWeights[i] /= totalInvRisk;
        }

        // Step 7: Map PC-space weights back to asset-space weights
        // w_asset = Σ_k (pcWeight_k * eigenvector_k)
        var assetWeights = new decimal[n];
        for (var i = 0; i < signalIndices.Count; i++)
        {
            var k = signalIndices[i];
            for (var j = 0; j < n; j++)
            {
                assetWeights[j] += pcWeights[i] * eigenvectors[j, k];
            }
        }

        // Step 8: Normalize sign — make largest absolute loading positive
        var maxAbsIdx = 0;
        var maxAbsVal = Math.Abs(assetWeights[0]);
        for (var j = 1; j < n; j++)
        {
            if (Math.Abs(assetWeights[j]) > maxAbsVal)
            {
                maxAbsVal = Math.Abs(assetWeights[j]);
                maxAbsIdx = j;
            }
        }

        if (assetWeights[maxAbsIdx] < 0m)
        {
            for (var j = 0; j < n; j++)
            {
                assetWeights[j] = -assetWeights[j];
            }
        }

        // Step 9: Clamp negatives to zero
        for (var j = 0; j < n; j++)
        {
            if (assetWeights[j] < 0m)
            {
                assetWeights[j] = 0m;
            }
        }

        // Step 10: Normalize to sum to 1
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

    private static decimal ComputeDefaultThreshold(decimal[][] returns, int n)
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
