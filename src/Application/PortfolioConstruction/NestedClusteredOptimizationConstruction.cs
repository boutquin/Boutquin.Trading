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
/// Nested Clustered Optimization (NCO), López de Prado (2019), "A Robust Estimator
/// of the Efficient Frontier" (SSRN 3469961, Snippet 4). A robust successor to plain
/// convex optimization that contains intra-cluster estimation instability within each
/// cluster so it does not propagate across the whole portfolio.
///
/// Four steps (V1 — min-variance for both the intra- and inter-cluster objective):
/// <list type="number">
///   <item>Cluster the correlation matrix. K-Means on the correlation distance
///   <c>d = sqrt((1 - rho) / 2)</c>; the number of clusters K is chosen by maximizing
///   the mean silhouette score over a deterministic K range.</item>
///   <item>Compute intra-cluster optimal weights <c>wIntra</c> per cluster
///   (minimum-variance <c>optPort(cov_c)</c> within each cluster).</item>
///   <item>Collapse to a reduced K×K covariance <c>covR = wIntraᵀ·cov·wIntra</c>
///   (≈ identity when clusters are uncorrelated) and optimize across clusters to get
///   <c>wInter</c> (minimum-variance again).</item>
///   <item>Final weights <c>w = wIntra · wInter</c> — each asset's intra-cluster weight
///   scaled by its cluster's inter-cluster weight.</item>
/// </list>
///
/// <para><b>Accuracy contract:</b> exact when the covariance is block-diagonal (clusters
/// uncorrelated): the inter-cluster optimizer then sees a near-identity reduced covariance,
/// so each cluster's inter-weight is its inverse-variance share and the nested solution
/// equals the analytically-correct per-cluster min-variance allocation scaled across clusters.
/// Requires invertible sub-covariances — pre-condition with RMT-denoising / Ledoit-Wolf
/// shrinkage at high N/T by injecting the corresponding <see cref="ICovarianceEstimator"/>.</para>
///
/// <para><b>Determinism:</b> both the silhouette-based K selection and the K-Means
/// assignment use a deterministic farthest-point ("k-means++ without randomness")
/// initialization so that results are reproducible and match the cross-language reference
/// vectors. See <c>generate_nco_vectors.py</c>.</para>
///
/// <para><b>Intended omissions (V1):</b> the MCOS Monte-Carlo model-selection harness
/// (a diagnostic, not the weights); negative-correlation dual-clustering (V1 clusters the
/// signed correlation, not its absolute value); a pluggable inter/intra objective (V1 uses
/// minimum-variance for both).</para>
/// </summary>
public sealed class NestedClusteredOptimizationConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly int _maxClusters;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="NestedClusteredOptimizationConstruction"/> class.
    /// </summary>
    /// <param name="covarianceEstimator">The covariance estimator. Defaults to <see cref="SampleCovarianceEstimator"/>.
    /// Inject a denoising / shrinkage estimator when N/T is high so the per-cluster sub-covariances stay invertible.</param>
    /// <param name="maxClusters">Upper bound on the candidate cluster count K scanned by the silhouette
    /// selection. Effective ceiling is <c>min(maxClusters, N - 1)</c>. Default 10.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (no floor).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    public NestedClusteredOptimizationConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        int maxClusters = 10,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m)
    {
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _maxClusters = maxClusters;
        _minWeight = minWeight;
        _maxWeight = maxWeight;
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

        var cov = _covarianceEstimator.Estimate(returns);

        // Step 1: cluster the correlation matrix (K-Means on correlation distance; K by silhouette).
        var labels = ClusterByCorrelation(cov, n);
        var clusters = GroupClusters(labels, n);

        // Step 2: intra-cluster minimum-variance weights for each cluster.
        var intraWeights = new decimal[n];
        foreach (var cluster in clusters)
        {
            var wIntra = MinVarWeights(SubMatrix(cov, cluster));
            for (var i = 0; i < cluster.Length; i++)
            {
                intraWeights[cluster[i]] = wIntra[i];
            }
        }

        // Step 3: collapse to a reduced K×K covariance covR = wIntraᵀ·cov·wIntra and
        // optimize across clusters (minimum-variance again) → inter-cluster weights.
        var reducedCov = ReducedCovariance(cov, clusters, intraWeights);
        var interWeights = MinVarWeights(reducedCov);

        // Step 4: final weights w = wIntra · wInter (intra weight × its cluster's inter weight).
        var w = new decimal[n];
        for (var c = 0; c < clusters.Count; c++)
        {
            foreach (var idx in clusters[c])
            {
                w[idx] = intraWeights[idx] * interWeights[c];
            }
        }

        // Normalize defensively (intra and inter weights each sum to 1, so the product already
        // sums to 1, but floating-point drift and weight clamps can perturb it slightly).
        Normalize(w);

        var weights = new Dictionary<Symbol, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            weights[assets[i]] = w[i];
        }

        return weights;
    }

    // ─── Step 1: clustering ──────────────────────────────────────────────────

    /// <summary>
    /// Clusters the assets by correlation distance using deterministic K-Means, choosing the
    /// number of clusters K that maximizes the mean silhouette score.
    /// </summary>
    private int[] ClusterByCorrelation(decimal[,] cov, int n)
    {
        var corr = ComputeCorrelationMatrix(cov, n);
        var dist = ComputeDistanceMatrix(corr, n);

        var maxK = Math.Min(_maxClusters, n - 1);
        if (maxK < 2)
        {
            // Fewer than two candidate clusters possible → single cluster.
            return new int[n];
        }

        var bestLabels = new int[n];
        var bestScore = decimal.MinValue;
        var found = false;

        for (var k = 2; k <= maxK; k++)
        {
            var labels = KMeans(dist, n, k);
            var actualK = labels.Distinct().Count();
            if (actualK < 2)
            {
                continue;
            }

            var score = MeanSilhouette(dist, labels, n);
            // Strict improvement keeps the smallest K on ties (deterministic).
            if (!found || score > bestScore + 1e-15m)
            {
                bestScore = score;
                bestLabels = labels;
                found = true;
            }
        }

        return found ? bestLabels : new int[n];
    }

    /// <summary>
    /// Deterministic K-Means over the correlation-distance space. Observations are the rows of the
    /// distance matrix (asset i is the vector of its distances to every other asset). Centroids are
    /// seeded by a deterministic farthest-point rule (k-means++ without randomness): the first
    /// centroid is the asset with the largest total distance to all others; each subsequent centroid
    /// is the asset farthest (max over min-distance-to-chosen) from the already-chosen centroids,
    /// ties broken by smallest index. Lloyd iterations then run to convergence.
    /// </summary>
    private static int[] KMeans(decimal[,] dist, int n, int k)
    {
        var seeds = SeedCentroids(dist, n, k);

        // Centroid coordinates in distance-row space (k × n).
        var centroids = new decimal[k][];
        for (var c = 0; c < k; c++)
        {
            centroids[c] = GetRow(dist, seeds[c], n);
        }

        var labels = new int[n];
        for (var iter = 0; iter < 100; iter++)
        {
            // Assignment step: nearest centroid by Euclidean distance in row space.
            var changed = false;
            for (var i = 0; i < n; i++)
            {
                var row = GetRow(dist, i, n);
                var best = 0;
                var bestD = decimal.MaxValue;
                for (var c = 0; c < k; c++)
                {
                    var d = SquaredEuclidean(row, centroids[c]);
                    if (d < bestD - 1e-18m)
                    {
                        bestD = d;
                        best = c;
                    }
                }

                if (labels[i] != best)
                {
                    labels[i] = best;
                    changed = true;
                }
            }

            // Update step: centroid = mean of assigned rows. Empty clusters keep their seed.
            var newCentroids = new decimal[k][];
            for (var c = 0; c < k; c++)
            {
                var members = new List<int>();
                for (var i = 0; i < n; i++)
                {
                    if (labels[i] == c)
                    {
                        members.Add(i);
                    }
                }

                if (members.Count == 0)
                {
                    newCentroids[c] = centroids[c];
                    continue;
                }

                var mean = new decimal[n];
                foreach (var m in members)
                {
                    var row = GetRow(dist, m, n);
                    for (var j = 0; j < n; j++)
                    {
                        mean[j] += row[j];
                    }
                }

                for (var j = 0; j < n; j++)
                {
                    mean[j] /= members.Count;
                }

                newCentroids[c] = mean;
            }

            centroids = newCentroids;

            if (!changed && iter > 0)
            {
                break;
            }
        }

        return labels;
    }

    /// <summary>
    /// Deterministic farthest-point seeding (k-means++ without randomness). Returns k distinct
    /// observation indices used as the initial centroids.
    /// </summary>
    private static int[] SeedCentroids(decimal[,] dist, int n, int k)
    {
        var seeds = new int[k];
        var chosen = new bool[n];

        // First seed: asset with the largest total distance to all others (most peripheral),
        // ties broken by smallest index.
        var first = 0;
        var maxTotal = decimal.MinValue;
        for (var i = 0; i < n; i++)
        {
            var total = 0m;
            for (var j = 0; j < n; j++)
            {
                total += dist[i, j];
            }

            if (total > maxTotal + 1e-18m)
            {
                maxTotal = total;
                first = i;
            }
        }

        seeds[0] = first;
        chosen[first] = true;

        for (var c = 1; c < k; c++)
        {
            // Pick the asset maximizing the minimum distance to any already-chosen centroid.
            var best = -1;
            var bestMinDist = decimal.MinValue;
            for (var i = 0; i < n; i++)
            {
                if (chosen[i])
                {
                    continue;
                }

                var minDist = decimal.MaxValue;
                for (var s = 0; s < c; s++)
                {
                    var d = dist[i, seeds[s]];
                    if (d < minDist)
                    {
                        minDist = d;
                    }
                }

                if (minDist > bestMinDist + 1e-18m)
                {
                    bestMinDist = minDist;
                    best = i;
                }
            }

            if (best < 0)
            {
                // Fewer distinct points than k: reuse the first seed (cluster will be empty).
                best = first;
            }

            seeds[c] = best;
            chosen[best] = true;
        }

        return seeds;
    }

    /// <summary>
    /// Mean silhouette score over all observations. For observation i with cluster A:
    /// a(i) = mean intra-cluster distance, b(i) = min over other clusters of the mean distance to
    /// that cluster, s(i) = (b - a) / max(a, b). Singletons score 0.
    /// </summary>
    private static decimal MeanSilhouette(decimal[,] dist, int[] labels, int n)
    {
        var k = labels.Max() + 1;
        var clusterMembers = new List<int>[k];
        for (var c = 0; c < k; c++)
        {
            clusterMembers[c] = new List<int>();
        }

        for (var i = 0; i < n; i++)
        {
            clusterMembers[labels[i]].Add(i);
        }

        var sum = 0m;
        for (var i = 0; i < n; i++)
        {
            var own = labels[i];
            if (clusterMembers[own].Count <= 1)
            {
                // Singleton silhouette is 0 by convention.
                continue;
            }

            var a = 0m;
            foreach (var j in clusterMembers[own])
            {
                if (j != i)
                {
                    a += dist[i, j];
                }
            }

            a /= clusterMembers[own].Count - 1;

            var b = decimal.MaxValue;
            for (var c = 0; c < k; c++)
            {
                if (c == own || clusterMembers[c].Count == 0)
                {
                    continue;
                }

                var meanToC = 0m;
                foreach (var j in clusterMembers[c])
                {
                    meanToC += dist[i, j];
                }

                meanToC /= clusterMembers[c].Count;
                if (meanToC < b)
                {
                    b = meanToC;
                }
            }

            if (b == decimal.MaxValue)
            {
                continue;
            }

            var denom = Math.Max(a, b);
            if (denom > 0m)
            {
                sum += (b - a) / denom;
            }
        }

        return sum / n;
    }

    private static List<int[]> GroupClusters(int[] labels, int n)
    {
        var byLabel = new SortedDictionary<int, List<int>>();
        for (var i = 0; i < n; i++)
        {
            if (!byLabel.TryGetValue(labels[i], out var list))
            {
                list = new List<int>();
                byLabel[labels[i]] = list;
            }

            list.Add(i);
        }

        return byLabel.Values.Select(v => v.ToArray()).ToList();
    }

    // ─── Steps 2/3: minimum-variance optimization ────────────────────────────

    /// <summary>
    /// Minimum-variance weights for a (sub-)covariance matrix: minimizes w'Σw subject to
    /// Σw = 1 and the weight bounds. Uses <see cref="ActiveSetQpSolver"/> with a projected
    /// gradient-descent fallback for non-positive-definite matrices, mirroring
    /// <see cref="MinimumVarianceConstruction"/>.
    /// </summary>
    private decimal[] MinVarWeights(decimal[,] cov)
    {
        var m = cov.GetLength(0);
        if (m == 1)
        {
            return new[] { 1m };
        }

        try
        {
            return ActiveSetQpSolver.SolveMinVariance(cov, _minWeight, _maxWeight);
        }
        catch (InvalidOperationException)
        {
            return SolveMinVarGradientDescent(cov, m);
        }
    }

    private decimal[] SolveMinVarGradientDescent(decimal[,] cov, int m)
    {
        var w = new decimal[m];
        for (var i = 0; i < m; i++)
        {
            w[i] = 1m / m;
        }

        for (var iter = 0; iter < 5000; iter++)
        {
            var grad = new decimal[m];
            for (var i = 0; i < m; i++)
            {
                for (var j = 0; j < m; j++)
                {
                    grad[i] += 2m * cov[i, j] * w[j];
                }
            }

            var stepped = false;
            var lr = 1.0m;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new decimal[m];
                for (var i = 0; i < m; i++)
                {
                    candidate[i] = w[i] - lr * grad[i];
                }

                ProjectOntoSimplex(candidate, _minWeight, _maxWeight);
                if (Variance(candidate, cov) < Variance(w, cov))
                {
                    var maxDiff = 0m;
                    for (var i = 0; i < m; i++)
                    {
                        maxDiff = Math.Max(maxDiff, Math.Abs(candidate[i] - w[i]));
                    }

                    w = candidate;
                    stepped = true;
                    if (maxDiff < 1e-12m)
                    {
                        return w;
                    }

                    break;
                }

                lr *= 0.5m;
            }

            if (!stepped)
            {
                break;
            }
        }

        return w;
    }

    /// <summary>
    /// Collapses the full covariance to a reduced K×K cluster covariance:
    /// <c>covR[a, b] = wIntra_aᵀ · cov · wIntra_b</c>, where <c>wIntra_c</c> is the intra-cluster
    /// weight vector embedded in the full asset space (zero outside its cluster).
    /// </summary>
    private static decimal[,] ReducedCovariance(decimal[,] cov, List<int[]> clusters, decimal[] intraWeights)
    {
        var k = clusters.Count;
        var reduced = new decimal[k, k];
        for (var a = 0; a < k; a++)
        {
            for (var b = 0; b < k; b++)
            {
                var sum = 0m;
                foreach (var i in clusters[a])
                {
                    foreach (var j in clusters[b])
                    {
                        sum += intraWeights[i] * cov[i, j] * intraWeights[j];
                    }
                }

                reduced[a, b] = sum;
            }
        }

        return reduced;
    }

    private static decimal[,] SubMatrix(decimal[,] cov, int[] indices)
    {
        var m = indices.Length;
        var sub = new decimal[m, m];
        for (var i = 0; i < m; i++)
        {
            for (var j = 0; j < m; j++)
            {
                sub[i, j] = cov[indices[i], indices[j]];
            }
        }

        return sub;
    }

    // ─── Shared numerics ─────────────────────────────────────────────────────

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
        // Correlation distance: d(i,j) = sqrt((1 - corr(i,j)) / 2).
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

    private static decimal[] GetRow(decimal[,] matrix, int row, int n)
    {
        var result = new decimal[n];
        for (var j = 0; j < n; j++)
        {
            result[j] = matrix[row, j];
        }

        return result;
    }

    private static decimal SquaredEuclidean(decimal[] a, decimal[] b)
    {
        var sum = 0m;
        for (var i = 0; i < a.Length; i++)
        {
            var d = a[i] - b[i];
            sum += d * d;
        }

        return sum;
    }

    private static decimal Variance(decimal[] w, decimal[,] cov)
    {
        var n = w.Length;
        var variance = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                variance += w[i] * w[j] * cov[i, j];
            }
        }

        return variance;
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

            var feasible = true;
            for (var i = 0; i < n; i++)
            {
                if (w[i] < minWeight - 1e-14m || w[i] > maxWeight + 1e-14m)
                {
                    feasible = false;
                    break;
                }
            }

            if (feasible)
            {
                return;
            }
        }
    }

    private static void Normalize(decimal[] w)
    {
        var sum = w.Sum();
        if (sum <= 0m)
        {
            for (var i = 0; i < w.Length; i++)
            {
                w[i] = 1m / w.Length;
            }

            return;
        }

        for (var i = 0; i < w.Length; i++)
        {
            w[i] /= sum;
        }
    }
}
