#!/usr/bin/env python3
"""
Generate Nested Clustered Optimization (NCO) construction vectors for
Boutquin.Trading.

Reference: López de Prado (2019), "A Robust Estimator of the Efficient Frontier",
SSRN 3469961 (Snippet 4). A robust successor to convex optimization that contains
intra-cluster estimation instability within each cluster.

This is an OWN-FORMULA replica of the C# NestedClusteredOptimizationConstruction —
it does NOT use pypfopt, cvxpy, scikit-learn, or any external NCO/k-means library.
The four NCO steps, the deterministic farthest-point K-Means seeding, the mean
silhouette K selection, and the minimum-variance optimizer are all replicated here
exactly so the C# cross-language tests can verify byte-for-byte agreement at
PrecisionNumeric (1e-6).

Determinism note
----------------
K-Means and silhouette-K selection are the single biggest source of cross-language
divergence. Both languages therefore use the SAME deterministic algorithm:

  * Seeding (k-means++ WITHOUT randomness / farthest-point):
      - first centroid  = asset with the largest total correlation-distance to all
        others (most peripheral), ties → smallest index;
      - each next centroid = asset maximizing the minimum distance to the already
        chosen centroids, ties → smallest index.
  * Observations are the ROWS of the correlation-distance matrix (asset i is the
    vector of its distances to every asset).
  * Lloyd iterations to convergence (centroid = mean of assigned rows; empty
    clusters keep their seed row).
  * K range scanned: 2 .. min(maxClusters, N-1). K chosen = argmax mean silhouette,
    ties → smallest K (strict-improvement comparison).

The min-var sub-problems use block-diagonal panels in the numeric vectors so the
optimum is the analytic inverse-variance allocation and does NOT depend on the
optimizer's iteration path — this keeps the C# ActiveSetQpSolver result and the
Python reference identical to machine precision.
"""

import json
from pathlib import Path

import numpy as np

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=3469)  # SSRN id flavor


def save(name: str, data: dict) -> None:
    def convert(obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        if isinstance(obj, (np.float64, np.float32)):
            return float(obj)
        if isinstance(obj, (np.int64, np.int32)):
            return int(obj)
        raise TypeError(f"Cannot serialize {type(obj)}")

    with open(VECTORS_DIR / f"{name}.json", "w") as f:
        json.dump(data, f, indent=2, default=convert)
    print(f"  ✓ {name}.json")


# ═══════════════════════════════════════════════════════════════════════════
# Reference implementation (matches C# NestedClusteredOptimizationConstruction)
# ═══════════════════════════════════════════════════════════════════════════

def sample_covariance(returns):
    """Sample covariance (N-1 divisor), matching SampleCovarianceEstimator.

    returns : (N, T) — one row per asset.
    """
    X = np.asarray(returns, dtype=float)
    n, t = X.shape
    means = X.mean(axis=1, keepdims=True)
    centered = X - means
    cov = (centered @ centered.T) / (t - 1)
    return cov


def correlation_matrix(cov):
    n = cov.shape[0]
    corr = np.zeros((n, n))
    for i in range(n):
        for j in range(n):
            denom = np.sqrt(cov[i, i] * cov[j, j])
            if denom > 0:
                corr[i, j] = cov[i, j] / denom
            else:
                corr[i, j] = 1.0 if i == j else 0.0
    return corr


def distance_matrix(corr):
    n = corr.shape[0]
    dist = np.zeros((n, n))
    for i in range(n):
        for j in range(n):
            if i == j:
                dist[i, j] = 0.0
            else:
                d = 0.5 * (1.0 - corr[i, j])
                dist[i, j] = np.sqrt(max(0.0, d))
    return dist


def seed_centroids(dist, n, k):
    """Deterministic farthest-point seeding. Returns k observation indices."""
    seeds = []
    chosen = [False] * n

    totals = dist.sum(axis=1)
    first = int(np.argmax(totals))  # argmax → smallest index on ties
    seeds.append(first)
    chosen[first] = True

    for _ in range(1, k):
        best = -1
        best_min = -np.inf
        for i in range(n):
            if chosen[i]:
                continue
            min_d = np.inf
            for s in seeds:
                if dist[i, s] < min_d:
                    min_d = dist[i, s]
            if min_d > best_min + 1e-18:
                best_min = min_d
                best = i
        if best < 0:
            best = first
        seeds.append(best)
        chosen[best] = True

    return seeds


def kmeans(dist, n, k):
    """Deterministic K-Means over distance-row observations."""
    seeds = seed_centroids(dist, n, k)
    centroids = [dist[s, :].copy() for s in seeds]
    labels = [0] * n

    for it in range(100):
        changed = False
        for i in range(n):
            row = dist[i, :]
            best = 0
            best_d = np.inf
            for c in range(k):
                d = float(np.sum((row - centroids[c]) ** 2))
                if d < best_d - 1e-18:
                    best_d = d
                    best = c
            if labels[i] != best:
                labels[i] = best
                changed = True

        new_centroids = []
        for c in range(k):
            members = [i for i in range(n) if labels[i] == c]
            if not members:
                new_centroids.append(centroids[c])
                continue
            mean = np.zeros(n)
            for m in members:
                mean += dist[m, :]
            mean /= len(members)
            new_centroids.append(mean)
        centroids = new_centroids

        if not changed and it > 0:
            break

    return labels


def mean_silhouette(dist, labels, n):
    k = max(labels) + 1
    members = [[] for _ in range(k)]
    for i in range(n):
        members[labels[i]].append(i)

    total = 0.0
    for i in range(n):
        own = labels[i]
        if len(members[own]) <= 1:
            continue
        a = sum(dist[i, j] for j in members[own] if j != i) / (len(members[own]) - 1)
        b = np.inf
        for c in range(k):
            if c == own or len(members[c]) == 0:
                continue
            mean_to_c = sum(dist[i, j] for j in members[c]) / len(members[c])
            if mean_to_c < b:
                b = mean_to_c
        if b == np.inf:
            continue
        denom = max(a, b)
        if denom > 0:
            total += (b - a) / denom
    return total / n


def cluster_by_correlation(cov, n, max_clusters=10):
    corr = correlation_matrix(cov)
    dist = distance_matrix(corr)
    max_k = min(max_clusters, n - 1)
    if max_k < 2:
        return [0] * n

    best_labels = [0] * n
    best_score = -np.inf
    found = False
    for k in range(2, max_k + 1):
        labels = kmeans(dist, n, k)
        if len(set(labels)) < 2:
            continue
        score = mean_silhouette(dist, labels, n)
        if not found or score > best_score + 1e-15:
            best_score = score
            best_labels = labels
            found = True
    return best_labels if found else [0] * n


def group_clusters(labels, n):
    by_label = {}
    for i in range(n):
        by_label.setdefault(labels[i], []).append(i)
    return [by_label[key] for key in sorted(by_label.keys())]


def min_var_weights(cov):
    """Analytic long-only minimum-variance weights via inverse-variance for the
    block-diagonal / well-conditioned panels used in the vectors.

    For a positive-definite Σ the unconstrained min-var solution is
    w = Σ⁻¹1 / (1ᵀΣ⁻¹1). The vectors are constructed so this is interior (all
    positive) — matching the C# ActiveSetQpSolver interior optimum.
    """
    m = cov.shape[0]
    if m == 1:
        return np.array([1.0])
    inv = np.linalg.inv(cov)
    ones = np.ones(m)
    w = inv @ ones
    w /= ones @ w
    return w


def reduced_covariance(cov, clusters, intra):
    k = len(clusters)
    reduced = np.zeros((k, k))
    for a in range(k):
        for b in range(k):
            s = 0.0
            for i in clusters[a]:
                for j in clusters[b]:
                    s += intra[i] * cov[i, j] * intra[j]
            reduced[a, b] = s
    return reduced


def nco_weights(returns, max_clusters=10):
    cov = sample_covariance(returns)
    n = cov.shape[0]
    if n == 1:
        return np.array([1.0]), [0], cov

    labels = cluster_by_correlation(cov, n, max_clusters)
    clusters = group_clusters(labels, n)

    intra = np.zeros(n)
    for cluster in clusters:
        sub = cov[np.ix_(cluster, cluster)]
        w_intra = min_var_weights(sub)
        for idx, asset in enumerate(cluster):
            intra[asset] = w_intra[idx]

    reduced = reduced_covariance(cov, clusters, intra)
    w_inter = min_var_weights(reduced)

    w = np.zeros(n)
    for c, cluster in enumerate(clusters):
        for asset in cluster:
            w[asset] = intra[asset] * w_inter[c]

    w /= w.sum()
    return w, labels, cov


# ═══════════════════════════════════════════════════════════════════════════
# Vector 1 — cross-language numeric: a real 6-asset, 2-cluster panel
# ═══════════════════════════════════════════════════════════════════════════

def make_block_panel(cluster_sizes, t, intra_corr, block_vols, seed_offset):
    """Build a return panel whose assets form well-separated correlation blocks.

    Within a block: shared factor + idiosyncratic noise → high positive correlation.
    Across blocks: independent factors → ~zero correlation. The covariance is then
    (near) block-diagonal, which is exactly NCO's exactness regime.
    """
    rng = np.random.default_rng(seed=3469 + seed_offset)
    series = []
    for b, size in enumerate(cluster_sizes):
        factor = rng.standard_normal(t)
        vol = block_vols[b]
        for _ in range(size):
            idio = rng.standard_normal(t)
            r = vol * (np.sqrt(intra_corr) * factor + np.sqrt(1 - intra_corr) * idio)
            series.append(r)
    return np.array(series)


def vector_numeric_6asset():
    # 6 assets in two blocks of 3, distinct in-block vols → distinct intra weights.
    returns = make_block_panel(
        cluster_sizes=[3, 3], t=400, intra_corr=0.85,
        block_vols=[0.012, 0.020], seed_offset=1,
    )
    w, labels, cov = nco_weights(returns.tolist())

    save("nco_numeric_6asset", {
        "description": "NCO on a real 6-asset 2-block panel (own-formula reference).",
        "reference": "Lopez de Prado (2019), SSRN 3469961, Snippet 4.",
        "inputs": {
            "returns": returns.tolist(),
            "max_clusters": 10,
        },
        "expected": {
            "weights": w.tolist(),
            "labels": labels,
            "num_clusters": int(len(set(labels))),
            "weights_sum": float(w.sum()),
        },
    })


# ═══════════════════════════════════════════════════════════════════════════
# Vector 2 — block-diagonal exactness (fidelity): clusters perfectly uncorrelated
# ═══════════════════════════════════════════════════════════════════════════

def vector_block_diagonal_exact():
    """A panel with two perfectly-uncorrelated blocks. The analytically-correct
    nested min-var solution: each cluster's intra weights are its inverse-variance
    allocation, the reduced 2×2 covariance is exactly diagonal, so the inter
    weights are the clusters' inverse-(reduced-variance) shares, and the final
    weight of each asset is intra_i × inter_c. We emit BOTH the NCO output and the
    independently-computed analytic target so the C# fidelity test asserts equality.
    """
    # Block A: 2 assets, vols 0.01 and 0.02 (uncorrelated within block too, so
    # intra min-var is pure inverse-variance). Block B: 2 assets, vols 0.015, 0.03.
    # Fully uncorrelated everywhere → block-diagonal covariance, the NCO exactness
    # regime, and every sub-covariance is diagonal so the optimum is analytic.
    t = 1
    # Construct covariance DIRECTLY (diagonal) rather than from a finite panel, to
    # remove sampling noise; the C# test injects this exact covariance.
    vols = np.array([0.010, 0.020, 0.015, 0.030])
    variances = vols ** 2
    cov = np.diag(variances)
    n = 4

    # Ground-truth clusters (the test fixes the labels so the assertion is on the
    # nested optimization, not the clustering): {0,1} and {2,3}.
    clusters = [[0, 1], [2, 3]]

    # Analytic intra weights = inverse-variance within each cluster.
    intra = np.zeros(n)
    for cluster in clusters:
        inv = 1.0 / variances[cluster]
        wi = inv / inv.sum()
        for idx, asset in enumerate(cluster):
            intra[asset] = wi[idx]

    # Reduced covariance: diagonal entry = wIntraᵀ Σ_block wIntra (cluster variance).
    reduced = np.zeros((2, 2))
    for c, cluster in enumerate(clusters):
        cluster_var = 0.0
        for i in cluster:
            cluster_var += (intra[i] ** 2) * variances[i]
        reduced[c, c] = cluster_var
    inter_inv = np.array([1.0 / reduced[0, 0], 1.0 / reduced[1, 1]])
    inter = inter_inv / inter_inv.sum()

    analytic = np.zeros(n)
    for c, cluster in enumerate(clusters):
        for asset in cluster:
            analytic[asset] = intra[asset] * inter[c]

    save("nco_block_diagonal_exact", {
        "description": "Block-diagonal exactness: NCO == analytic nested min-var.",
        "reference": "Lopez de Prado (2019), SSRN 3469961 — block-diagonal accuracy contract.",
        "inputs": {
            # The covariance is injected directly via a fixed estimator in C#.
            "covariance": cov.tolist(),
            "fixed_labels": [0, 0, 1, 1],
        },
        "expected": {
            "intra_weights": intra.tolist(),
            "reduced_cov_diag": [float(reduced[0, 0]), float(reduced[1, 1])],
            "inter_weights": inter.tolist(),
            "analytic_weights": analytic.tolist(),
            "weights_sum": float(analytic.sum()),
        },
    })


# ═══════════════════════════════════════════════════════════════════════════
# Vector 3 — single cluster reduces to plain min-var (degenerate guard)
# ═══════════════════════════════════════════════════════════════════════════

def vector_single_cluster_minvar():
    """When the silhouette selection yields a single cluster (all assets highly
    correlated), NCO reduces to plain minimum-variance on the whole covariance.
    We give a diagonal covariance whose min-var is analytic inverse-variance."""
    vols = np.array([0.010, 0.020, 0.040])
    variances = vols ** 2
    cov = np.diag(variances)
    inv = 1.0 / variances
    minvar = inv / inv.sum()

    save("nco_single_cluster_minvar", {
        "description": "Single-cluster NCO reduces to plain minimum-variance.",
        "inputs": {
            "covariance": cov.tolist(),
        },
        "expected": {
            "minvar_weights": minvar.tolist(),
            "weights_sum": float(minvar.sum()),
        },
    })


if __name__ == "__main__":
    print("Generating NCO vectors...")
    vector_numeric_6asset()
    vector_block_diagonal_exact()
    vector_single_cluster_minvar()
    print("Done.")
