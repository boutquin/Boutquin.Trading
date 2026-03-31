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
/// Hierarchical Equal Risk Contribution (Raffinot, 2018).
/// Same three-step algorithm as HRP (Lopez de Prado, 2016):
/// 1. Cluster assets by correlation distance using single-linkage agglomerative clustering
/// 2. Reorder covariance matrix by dendrogram (quasi-diagonal)
/// 3. Recursive bisection — but with inverse-risk (1/σ) allocation instead of
///    HRP's inverse-variance (1/σ²), producing more balanced risk contributions.
///
/// Using standard deviation rather than variance gives each cluster equal
/// marginal risk contribution at the bisection level.
/// </summary>
public sealed class HierarchicalEqualRiskContributionConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchicalEqualRiskContributionConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    public HierarchicalEqualRiskContributionConstruction(
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

        // Step 3: Recursive bisection with inverse-risk (1/σ) allocation
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m;
        }

        RecursiveBisection(sortedIndices, cov, w);

        // Apply weight constraints via iterative clamping
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
    /// Recursive bisection with inverse-risk (1/σ) allocation.
    /// Unlike HRP which uses inverse-variance (1/σ²), HERC uses inverse standard
    /// deviation to produce equal risk contributions between clusters at each split.
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

        // HERC: inverse standard deviation allocation (not inverse variance)
        var riskLeft = (decimal)Math.Sqrt(Math.Max(0.0, (double)varLeft));
        var riskRight = (decimal)Math.Sqrt(Math.Max(0.0, (double)varRight));

        var totalInvRisk = 0m;
        if (riskLeft > 0m)
        {
            totalInvRisk += 1m / riskLeft;
        }

        if (riskRight > 0m)
        {
            totalInvRisk += 1m / riskRight;
        }

        decimal alphaLeft;
        if (totalInvRisk > 0m && riskLeft > 0m)
        {
            alphaLeft = (1m / riskLeft) / totalInvRisk;
        }
        else
        {
            alphaLeft = 0.5m;
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

        RecursiveBisection(left, cov, weights);
        RecursiveBisection(right, cov, weights);
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
}
