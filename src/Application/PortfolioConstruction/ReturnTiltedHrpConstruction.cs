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
/// Return-Tilted Hierarchical Risk Parity (Lohre, Rother, Schafer 2020).
/// Extends standard HRP by blending a return signal into the recursive bisection step.
/// At each split, cluster allocation is:
///   alpha_tilted = (1 - kappa) * alpha_risk + kappa * alpha_return
/// where alpha_risk is the standard inverse-variance allocation and alpha_return
/// tilts toward the cluster with higher mean returns.
///
/// kappa = 0 recovers pure HRP. kappa = 1 allocates purely by returns.
/// Never inverts the covariance matrix (numerically stable).
/// </summary>
public sealed class ReturnTiltedHrpConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _kappa;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnTiltedHrpConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="kappa">Return tilt strength in [0, 1]. 0 = pure HRP, 1 = pure return-based. Default 0.5.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    public ReturnTiltedHrpConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal kappa = 0.5m,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m)
    {
        if (kappa < 0m || kappa > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(kappa), kappa, "Kappa must be between 0 and 1 inclusive.");
        }

        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _kappa = kappa;
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

        if (n == 1)
        {
            return new Dictionary<Asset, decimal> { [assets[0]] = 1m };
        }

        // Compute per-asset mean returns for the return tilt signal.
        var meanReturns = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            var series = returns[i];
            meanReturns[i] = series.Length > 0 ? series.Average() : 0m;
        }

        var cov = _covarianceEstimator.Estimate(returns);

        // Step 1: Compute correlation distance matrix
        var corr = ComputeCorrelationMatrix(cov, n);
        var dist = ComputeDistanceMatrix(corr, n);

        // Step 2: Single-linkage agglomerative clustering -> dendrogram order
        var sortedIndices = ClusterAndReorder(dist, n);

        // Step 3: Return-tilted recursive bisection
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m;
        }

        RecursiveBisection(sortedIndices, cov, meanReturns, _kappa, w);

        // Apply weight constraints via iterative clamping.
        var effectiveMax = Math.Max(_maxWeight, 1m / n);
        var effectiveMin = Math.Min(_minWeight, 1m / n);
        for (var round = 0; round < 50; round++)
        {
            for (var i = 0; i < n; i++)
            {
                w[i] = Math.Max(effectiveMin, Math.Min(effectiveMax, w[i]));
            }

            var clampSum = w.Sum();
            if (clampSum <= 0m)
            {
                break;
            }

            for (var i = 0; i < n; i++)
            {
                w[i] /= clampSum;
            }

            var feasible = true;
            for (var i = 0; i < n; i++)
            {
                if (w[i] < effectiveMin - 1e-14m || w[i] > effectiveMax + 1e-14m)
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
            weights[assets[i]] = w[i];
        }

        return weights;
    }

    private static decimal[,] ComputeCorrelationMatrix(decimal[,] cov, int n)
    {
        var corr = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var denom = (decimal)Math.Sqrt((double)(cov[i, i] * cov[j, j]));
                corr[i, j] = denom > 0m ? cov[i, j] / denom : (i == j ? 1m : 0m);
            }
        }

        return corr;
    }

    private static decimal[,] ComputeDistanceMatrix(decimal[,] corr, int n)
    {
        var dist = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (i == j)
                {
                    dist[i, j] = 0m;
                }
                else
                {
                    var d = 0.5m * (1m - corr[i, j]);
                    dist[i, j] = (decimal)Math.Sqrt(Math.Max(0.0, (double)d));
                }
            }
        }

        return dist;
    }

    private static int[] ClusterAndReorder(decimal[,] dist, int n)
    {
        var clusters = new List<List<int>>(n);
        for (var i = 0; i < n; i++)
        {
            clusters.Add([i]);
        }

        var active = new List<int>(Enumerable.Range(0, n));

        var clusterDist = new decimal[n, n];
        Array.Copy(dist, clusterDist, dist.Length);

        while (active.Count > 1)
        {
            var minDist = decimal.MaxValue;
            var minI = -1;
            var minJ = -1;

            for (var ii = 0; ii < active.Count; ii++)
            {
                for (var jj = ii + 1; jj < active.Count; jj++)
                {
                    var d = clusterDist[active[ii], active[jj]];
                    if (d < minDist)
                    {
                        minDist = d;
                        minI = ii;
                        minJ = jj;
                    }
                }
            }

            var ci = active[minI];
            var cj = active[minJ];

            clusters[ci].AddRange(clusters[cj]);

            foreach (var k in active)
            {
                if (k == ci || k == cj)
                {
                    continue;
                }

                clusterDist[ci, k] = Math.Min(clusterDist[ci, k], clusterDist[cj, k]);
                clusterDist[k, ci] = clusterDist[ci, k];
            }

            active.RemoveAt(minJ);
        }

        return clusters[active[0]].ToArray();
    }

    /// <summary>
    /// Return-tilted recursive bisection. Blends inverse-variance allocation with
    /// a return-based signal at each split:
    ///   alpha = (1 - kappa) * alpha_risk + kappa * alpha_return
    /// Falls back to pure inverse-variance when return signal is non-positive for both clusters.
    /// </summary>
    private static void RecursiveBisection(
        int[] sortedIndices,
        decimal[,] cov,
        decimal[] meanReturns,
        decimal kappa,
        decimal[] weights)
    {
        if (sortedIndices.Length <= 1)
        {
            return;
        }

        var mid = sortedIndices.Length / 2;
        var left = sortedIndices[..mid];
        var right = sortedIndices[mid..];

        // Risk-based allocation (standard HRP inverse-variance)
        var varLeft = ComputeClusterVariance(left, cov);
        var varRight = ComputeClusterVariance(right, cov);

        var totalInvVar = 0m;
        if (varLeft > 0m)
        {
            totalInvVar += 1m / varLeft;
        }

        if (varRight > 0m)
        {
            totalInvVar += 1m / varRight;
        }

        decimal alphaRisk;
        if (totalInvVar > 0m && varLeft > 0m)
        {
            alphaRisk = (1m / varLeft) / totalInvVar;
        }
        else
        {
            alphaRisk = 0.5m;
        }

        // Return-based allocation (softmax-normalized cluster mean returns)
        var returnLeft = ComputeClusterMeanReturn(left, meanReturns);
        var returnRight = ComputeClusterMeanReturn(right, meanReturns);

        decimal alphaLeft;
        if (kappa > 0m)
        {
            // Use softmax normalization: exp(r_i) / sum(exp(r_j))
            // Softmax handles negative returns gracefully and always produces valid weights.
            var expLeft = (decimal)Math.Exp((double)returnLeft);
            var expRight = (decimal)Math.Exp((double)returnRight);
            var expSum = expLeft + expRight;

            var alphaReturn = expSum > 0m ? expLeft / expSum : 0.5m;

            alphaLeft = (1m - kappa) * alphaRisk + kappa * alphaReturn;
        }
        else
        {
            // kappa == 0: pure risk-based
            alphaLeft = alphaRisk;
        }

        var alphaRight = 1m - alphaLeft;

        foreach (var idx in left)
        {
            weights[idx] *= alphaLeft;
        }

        foreach (var idx in right)
        {
            weights[idx] *= alphaRight;
        }

        RecursiveBisection(left, cov, meanReturns, kappa, weights);
        RecursiveBisection(right, cov, meanReturns, kappa, weights);
    }

    private static decimal ComputeClusterVariance(int[] indices, decimal[,] cov)
    {
        var n = indices.Length;
        if (n == 0)
        {
            return 0m;
        }

        var w = 1m / n;
        var variance = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                variance += w * w * cov[indices[i], indices[j]];
            }
        }

        return variance;
    }

    private static decimal ComputeClusterMeanReturn(int[] indices, decimal[] meanReturns)
    {
        if (indices.Length == 0)
        {
            return 0m;
        }

        var sum = 0m;
        foreach (var idx in indices)
        {
            sum += meanReturns[idx];
        }

        return sum / indices.Length;
    }
}
