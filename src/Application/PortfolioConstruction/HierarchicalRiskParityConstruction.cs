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
/// Hierarchical Risk Parity (Lopez de Prado, 2016).
/// Three-step algorithm:
/// 1. Cluster assets by correlation distance using single-linkage agglomerative clustering
/// 2. Reorder covariance matrix by dendrogram (quasi-diagonal)
/// 3. Recursive bisection with inverse-variance allocation within clusters
///
/// Never fails numerically (no matrix inversion required).
/// </summary>
public sealed class HierarchicalRiskParityConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchicalRiskParityConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    public HierarchicalRiskParityConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m)
    {
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

        if (n == 1)
        {
            return new Dictionary<Asset, decimal> { [assets[0]] = 1m };
        }

        var cov = _covarianceEstimator.Estimate(returns);

        // Step 1: Compute correlation distance matrix
        var corr = ComputeCorrelationMatrix(cov, n);
        var dist = ComputeDistanceMatrix(corr, n);

        // Step 2: Single-linkage agglomerative clustering → dendrogram order
        var sortedIndices = ClusterAndReorder(dist, n);

        // Step 3: Recursive bisection with inverse-variance allocation
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m;
        }

        RecursiveBisection(sortedIndices, cov, w);

        // Apply weight constraints via iterative clamping.
        // Auto-relax constraints when infeasible: with N assets, maxWeight must be >= 1/N
        // and minWeight must be <= 1/N, otherwise weights can't sum to 1.0.
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
        // Correlation distance: d(i,j) = sqrt(0.5 * (1 - corr(i,j)))
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

    /// <summary>
    /// Single-linkage agglomerative clustering that produces a quasi-diagonal ordering.
    /// Returns the reordered asset indices following the dendrogram leaf order.
    /// </summary>
    private static int[] ClusterAndReorder(decimal[,] dist, int n)
    {
        // Each node starts as its own cluster, represented by its leaf order
        var clusters = new List<List<int>>(n);
        for (var i = 0; i < n; i++)
        {
            clusters.Add([i]);
        }

        // Distance between clusters (condensed). We track active cluster indices.
        var active = new List<int>(Enumerable.Range(0, n));

        // Build a mutable copy of the distance matrix for cluster merging
        var clusterDist = new decimal[n, n];
        Array.Copy(dist, clusterDist, dist.Length);

        while (active.Count > 1)
        {
            // Find the pair with minimum distance
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

            // Merge: append cluster j into cluster i
            clusters[ci].AddRange(clusters[cj]);

            // Update distances using single-linkage (minimum)
            foreach (var k in active)
            {
                if (k == ci || k == cj)
                {
                    continue;
                }

                clusterDist[ci, k] = Math.Min(clusterDist[ci, k], clusterDist[cj, k]);
                clusterDist[k, ci] = clusterDist[ci, k];
            }

            // Remove cluster j from active
            active.RemoveAt(minJ);
        }

        return clusters[active[0]].ToArray();
    }

    /// <summary>
    /// Recursive bisection: splits the sorted indices in half, computes cluster variance
    /// for each half, and allocates using inverse-variance weighting.
    /// </summary>
    private static void RecursiveBisection(int[] sortedIndices, decimal[,] cov, decimal[] weights)
    {
        if (sortedIndices.Length <= 1)
        {
            return;
        }

        var mid = sortedIndices.Length / 2;
        var left = sortedIndices[..mid];
        var right = sortedIndices[mid..];

        var varLeft = ComputeClusterVariance(left, cov);
        var varRight = ComputeClusterVariance(right, cov);

        // Inverse-variance allocation between the two clusters
        var totalInvVar = 0m;
        if (varLeft > 0m)
        {
            totalInvVar += 1m / varLeft;
        }

        if (varRight > 0m)
        {
            totalInvVar += 1m / varRight;
        }

        decimal alphaLeft;
        if (totalInvVar > 0m && varLeft > 0m)
        {
            alphaLeft = (1m / varLeft) / totalInvVar;
        }
        else
        {
            alphaLeft = 0.5m;
        }

        var alphaRight = 1m - alphaLeft;

        // Scale weights for each cluster
        foreach (var idx in left)
        {
            weights[idx] *= alphaLeft;
        }

        foreach (var idx in right)
        {
            weights[idx] *= alphaRight;
        }

        // Recurse
        RecursiveBisection(left, cov, weights);
        RecursiveBisection(right, cov, weights);
    }

    /// <summary>
    /// Computes the variance of an equal-weight sub-portfolio for the given cluster indices.
    /// This is the standard HRP cluster variance: w'Σw where w = 1/n for the cluster.
    /// </summary>
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
}
