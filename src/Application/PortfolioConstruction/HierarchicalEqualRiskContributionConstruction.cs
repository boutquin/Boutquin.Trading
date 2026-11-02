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
/// Hierarchical Equal Risk Contribution (HERC) portfolio — a faithful implementation of
/// Raffinot (2018), <i>"The Hierarchical Equal Risk Contribution Portfolio"</i>
/// (SSRN 3237540; <i>Journal of Alternative Investments</i>, 2018). HERC merges the machine-learning
/// hierarchical-clustering approach with the top-down recursive bisection of HRP, but — unlike HRP —
/// it splits by the <b>shape of the dendrogram</b> rather than the number-of-assets midpoint, selects
/// the relevant number of clusters, and allocates between clusters by <b>equal risk contribution</b>.
///
/// <para>
/// It is computed in four stages (Raffinot, §"The HERC Portfolio"):
/// </para>
/// <list type="number">
///   <item><description><b>Hierarchical clustering</b> — single-linkage agglomerative clustering on the
///     correlation distance <c>d(i,j) = √(½·(1 − ρ_ij))</c>, retaining the full dendrogram (merge tree).</description></item>
///   <item><description><b>Selection of the optimal number of clusters</b> via the Gap index
///     (Tibshirani et&#160;al. 2001): the observed within-cluster dispersion is compared against the
///     dispersion of uniformly-distributed reference data (with no clustering structure), and the
///     smallest cluster count <c>k</c> with <c>Gap(k) ≥ Gap(k+1) − s_{k+1}</c> is chosen.</description></item>
///   <item><description><b>Top-down recursive division into two parts based on the dendrogram shape</b>
///     (not the number-of-assets midpoint), following an Equal-Risk-Contribution allocation between the
///     two cluster groups: <c>a₁ = RC₂/(RC₁ + RC₂)</c> where <c>RC_i = σ_i</c> is the cluster's risk
///     (Raffinot treats clusters as uncorrelated), so the two groups contribute equal risk.</description></item>
///   <item><description><b>Naive risk parity within</b> each cluster — inverse-variance weighting of the
///     cluster's members.</description></item>
/// </list>
///
/// <para>
/// Never inverts the covariance matrix; numerically stable for near-singular Σ. The Gap-index reference
/// draws are driven by a deterministic LCG so the algorithm is reproducible and matches the
/// own-formula cross-language verification vector bit-for-bit.
/// </para>
///
/// <para><b>Reference:</b> Raffinot, T. (2018). <i>The Hierarchical Equal Risk Contribution Portfolio</i>.
/// SSRN 3237540; <i>Journal of Alternative Investments</i>. Cluster-count selection: Tibshirani, R.,
/// Walther, G., and Hastie, T. (2001). <i>Estimating the number of clusters in a data set via the gap
/// statistic</i>. <i>Journal of the Royal Statistical Society, Series B</i>, 63(2), 411–423.</para>
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

        // Step 1: Hierarchical clustering — correlation distance + single-linkage dendrogram (merge tree).
        var corr = ComputeCorrelationMatrix(cov, n);
        var dist = ComputeDistanceMatrix(corr, n);
        var (merges, members, root) = BuildLinkage(dist, n);
        var leafOrder = members[root];

        // Step 2: Selection of the optimal number of clusters via the Gap index.
        var k = GapIndexNumClusters(dist, n, merges, members, root);

        // Cut the dendrogram into k flat clusters, ordered by the dendrogram leaf order.
        var pos = new Dictionary<int, int>(n);
        for (var i = 0; i < leafOrder.Count; i++)
        {
            pos[leafOrder[i]] = i;
        }

        var flat = CutTree(root, n, merges, members, k);
        flat.Sort((a, b) => MinPos(a, pos).CompareTo(MinPos(b, pos)));

        // Step 3: Top-down ERC division between clusters (dendrogram shape).
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m;
        }

        HercAllocate(flat, cov, w);

        // Step 4: Naive risk parity (inverse-variance) within each cluster.
        foreach (var cluster in flat)
        {
            if (cluster.Count <= 1)
            {
                continue;
            }

            var inner = NaiveRiskParityWeights(cluster, cov);
            for (var idx = 0; idx < cluster.Count; idx++)
            {
                w[cluster[idx]] *= inner[idx];
            }
        }

        // Apply weight constraints via iterative clamping (auto-relax when infeasible).
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

        var weights = new Dictionary<Symbol, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            weights[assets[i]] = w[i];
        }

        return weights;
    }

    private static int MinPos(List<int> cluster, Dictionary<int, int> pos)
    {
        var min = int.MaxValue;
        foreach (var i in cluster)
        {
            if (pos[i] < min)
            {
                min = pos[i];
            }
        }

        return min;
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
    /// Single-linkage agglomerative clustering that retains the full merge tree (dendrogram).
    /// Leaves are <c>0..n-1</c>; internal nodes are <c>n..2n-2</c> in ascending merge order.
    /// Returns the per-merge child pair, the leaf members of every node, and the root node id.
    /// </summary>
    private static (List<(int Left, int Right)> Merges, Dictionary<int, List<int>> Members, int Root)
        BuildLinkage(decimal[,] dist, int n)
    {
        var members = new Dictionary<int, List<int>>(2 * n);
        for (var i = 0; i < n; i++)
        {
            members[i] = [i];
        }

        // active cluster slot -> node id
        var nodeOfCluster = new Dictionary<int, int>(n);
        for (var i = 0; i < n; i++)
        {
            nodeOfCluster[i] = i;
        }

        var active = new List<int>(Enumerable.Range(0, n));

        var clusterDist = new decimal[n, n];
        Array.Copy(dist, clusterDist, dist.Length);

        var merges = new List<(int, int)>(n - 1);
        var nextNode = n;

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
            var nodeI = nodeOfCluster[ci];
            var nodeJ = nodeOfCluster[cj];

            merges.Add((nodeI, nodeJ));

            var merged = new List<int>(members[nodeI].Count + members[nodeJ].Count);
            merged.AddRange(members[nodeI]);
            merged.AddRange(members[nodeJ]);
            members[nextNode] = merged;

            foreach (var kk in active)
            {
                if (kk == ci || kk == cj)
                {
                    continue;
                }

                clusterDist[ci, kk] = Math.Min(clusterDist[ci, kk], clusterDist[cj, kk]);
                clusterDist[kk, ci] = clusterDist[ci, kk];
            }

            nodeOfCluster[ci] = nextNode;
            active.RemoveAt(minJ);
            nextNode++;
        }

        return (merges, members, nodeOfCluster[active[0]]);
    }

    private static (int Left, int Right)? NodeChildren(int node, int n, List<(int Left, int Right)> merges)
    {
        if (node < n)
        {
            return null;
        }

        return merges[node - n];
    }

    /// <summary>
    /// Cut the dendrogram so exactly <paramref name="k"/> flat clusters remain. Repeatedly split the
    /// frontier node with the largest id (the latest-formed merge = greatest dendrogram height) until
    /// <paramref name="k"/> clusters exist. Returns each cluster as its list of leaf members.
    /// </summary>
    private static List<List<int>> CutTree(
        int root, int n, List<(int Left, int Right)> merges, Dictionary<int, List<int>> members, int k)
    {
        var frontier = new List<int> { root };
        while (frontier.Count < k)
        {
            var split = -1;
            foreach (var nd in frontier)
            {
                if (nd >= n && nd > split)
                {
                    split = nd;
                }
            }

            if (split < 0)
            {
                break;
            }

            frontier.Remove(split);
            var children = NodeChildren(split, n, merges)!.Value;
            frontier.Add(children.Left);
            frontier.Add(children.Right);
        }

        var result = new List<List<int>>(frontier.Count);
        foreach (var nd in frontier)
        {
            result.Add(members[nd]);
        }

        return result;
    }

    /// <summary>
    /// Numerical-Recipes <c>ranqd1</c> linear congruential generator, replicated identically in the
    /// Python reference so the Gap-index uniform reference draws — and therefore the selected cluster
    /// count — match bit-for-bit across languages.
    /// </summary>
    private sealed class Lcg(uint seed)
    {
        private uint _state = seed;

        public double NextUnit()
        {
            _state = (1664525u * _state) + 1013904223u;
            return _state / 4294967296.0;
        }
    }

    /// <summary>
    /// Tibshirani et&#160;al. (2001) Gap statistic → optimal number of clusters. Within-cluster
    /// dispersion is the pooled intra-cluster sum of squared pairwise distances divided by cluster size;
    /// the reference is <c>B</c> uniform distance matrices over the observed off-diagonal distance range
    /// (deterministic LCG), clustered the same way. Returns the smallest <c>k</c> with
    /// <c>Gap(k) ≥ Gap(k+1) − s_{k+1}</c>.
    /// </summary>
    private static int GapIndexNumClusters(
        decimal[,] dist, int n, List<(int Left, int Right)> merges, Dictionary<int, List<int>> members, int root)
    {
        if (n <= 2)
        {
            return n;
        }

        var kmax = n;

        var logWObs = new double[kmax];
        for (var k = 1; k <= kmax; k++)
        {
            var clusters = CutTree(root, n, merges, members, k);
            var wk = Dispersion(clusters, dist);
            logWObs[k - 1] = wk > 0 ? Math.Log(wk) : 0.0;
        }

        // Observed off-diagonal distance range
        var dmin = double.MaxValue;
        var dmax = double.MinValue;
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var v = (double)dist[i, j];
                if (v < dmin)
                {
                    dmin = v;
                }

                if (v > dmax)
                {
                    dmax = v;
                }
            }
        }

        const int b = 10;
        var rng = new Lcg(982451653u);
        var refLogW = new double[kmax][];
        for (var k = 0; k < kmax; k++)
        {
            refLogW[k] = new double[b];
        }

        for (var draw = 0; draw < b; draw++)
        {
            var rdist = new decimal[n, n];
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var u = rng.NextUnit();
                    var val = (decimal)(dmin + ((dmax - dmin) * u));
                    rdist[i, j] = val;
                    rdist[j, i] = val;
                }
            }

            var (rmerges, rmembers, rroot) = BuildLinkage(rdist, n);
            for (var k = 1; k <= kmax; k++)
            {
                var rclusters = CutTree(rroot, n, rmerges, rmembers, k);
                var wk = Dispersion(rclusters, rdist);
                refLogW[k - 1][draw] = wk > 0 ? Math.Log(wk) : 0.0;
            }
        }

        var gap = new double[kmax];
        var sk = new double[kmax];
        for (var k = 0; k < kmax; k++)
        {
            var mean = 0.0;
            for (var t = 0; t < b; t++)
            {
                mean += refLogW[k][t];
            }

            mean /= b;
            gap[k] = mean - logWObs[k];

            var variance = 0.0;
            for (var t = 0; t < b; t++)
            {
                var diff = refLogW[k][t] - mean;
                variance += diff * diff;
            }

            variance /= b;
            sk[k] = Math.Sqrt(variance) * Math.Sqrt(1.0 + (1.0 / b));
        }

        for (var k = 0; k < kmax - 1; k++)
        {
            if (gap[k] >= gap[k + 1] - sk[k + 1])
            {
                return k + 1;
            }
        }

        return kmax;
    }

    /// <summary>
    /// Pooled within-cluster dispersion: for each cluster, the sum of intra-cluster squared pairwise
    /// distances divided by the cluster size (Tibshirani's <c>W_k</c> form, the factor of 2 folded out).
    /// </summary>
    private static double Dispersion(List<List<int>> clusters, decimal[,] dist)
    {
        var w = 0.0;
        foreach (var c in clusters)
        {
            var m = c.Count;
            if (m <= 1)
            {
                continue;
            }

            var s = 0.0;
            for (var a = 0; a < m; a++)
            {
                for (var bb = a + 1; bb < m; bb++)
                {
                    var d = (double)dist[c[a], c[bb]];
                    s += d * d;
                }
            }

            w += s / m;
        }

        return w;
    }

    /// <summary>
    /// Step 3: recursively bisect the ordered list of clusters into two groups and allocate between the
    /// groups by equal risk contribution (<c>a_left = σ_right/(σ_left + σ_right)</c>), where each group's
    /// risk is the std-dev of its naive-risk-parity sub-portfolio. Recurse into each group.
    /// </summary>
    private static void HercAllocate(List<List<int>> clusters, decimal[,] cov, decimal[] weights)
    {
        if (clusters.Count <= 1)
        {
            return;
        }

        var mid = clusters.Count / 2;
        var left = clusters.GetRange(0, mid);
        var right = clusters.GetRange(mid, clusters.Count - mid);

        var leftMembers = Flatten(left);
        var rightMembers = Flatten(right);

        var riskLeft = ClusterRisk(leftMembers, cov);
        var riskRight = ClusterRisk(rightMembers, cov);

        var total = riskLeft + riskRight;
        decimal alphaLeft;
        if (total > 0m)
        {
            // ERC: the lower-risk group gets the larger weight so the two contribute equal risk.
            alphaLeft = riskRight / total;
        }
        else
        {
            alphaLeft = 0.5m;
        }

        var alphaRight = 1m - alphaLeft;

        foreach (var i in leftMembers)
        {
            weights[i] *= alphaLeft;
        }

        foreach (var i in rightMembers)
        {
            weights[i] *= alphaRight;
        }

        HercAllocate(left, cov, weights);
        HercAllocate(right, cov, weights);
    }

    private static List<int> Flatten(List<List<int>> clusters)
    {
        var result = new List<int>();
        foreach (var c in clusters)
        {
            result.AddRange(c);
        }

        return result;
    }

    /// <summary>
    /// Std-dev of the naive-risk-parity (inverse-variance) sub-portfolio over the given members.
    /// </summary>
    private static decimal ClusterRisk(List<int> members, decimal[,] cov)
    {
        var variance = NaiveRiskParityVariance(members, cov);
        return (decimal)Math.Sqrt(Math.Max(0.0, (double)variance));
    }

    /// <summary>
    /// Inverse-variance (naive-risk-parity) weights over the cluster members, normalized to sum to 1.
    /// </summary>
    private static decimal[] NaiveRiskParityWeights(List<int> members, decimal[,] cov)
    {
        var m = members.Count;
        var inv = new decimal[m];
        var sum = 0m;
        for (var i = 0; i < m; i++)
        {
            var variance = cov[members[i], members[i]];
            inv[i] = variance > 0m ? 1m / variance : 0m;
            sum += inv[i];
        }

        var w = new decimal[m];
        if (sum > 0m)
        {
            for (var i = 0; i < m; i++)
            {
                w[i] = inv[i] / sum;
            }
        }
        else
        {
            for (var i = 0; i < m; i++)
            {
                w[i] = 1m / m;
            }
        }

        return w;
    }

    /// <summary>
    /// Variance <c>w'Σw</c> of the inverse-variance (naive-risk-parity) sub-portfolio of a cluster.
    /// </summary>
    private static decimal NaiveRiskParityVariance(List<int> members, decimal[,] cov)
    {
        var m = members.Count;
        if (m == 0)
        {
            return 0m;
        }

        if (m == 1)
        {
            return cov[members[0], members[0]];
        }

        var w = NaiveRiskParityWeights(members, cov);
        var variance = 0m;
        for (var i = 0; i < m; i++)
        {
            for (var j = 0; j < m; j++)
            {
                variance += w[i] * w[j] * cov[members[i], members[j]];
            }
        }

        return variance;
    }
}
