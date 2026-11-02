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

using Boutquin.Trading.Domain.Exceptions;

using NumericsStats = Boutquin.Numerics.Statistics;

namespace Boutquin.Trading.Application.CovarianceEstimators;

/// <summary>
/// Covariance estimator based on the <b>Factor Graphical Lasso</b> of Lee &amp; Seregina
/// (2020/2023) — a low-dimensional factor model whose <i>residual</i> precision is estimated
/// by a sparse (graphical-lasso) precision estimator, with the full precision reassembled via
/// the Sherman–Morrison–Woodbury identity and inverted to a covariance.
/// </summary>
/// <remarks>
/// <para>
/// The estimator decomposes the demeaned <c>T × N</c> return panel through the approximate
/// factor model <c>r̃_t = m + B f_t + ε_t</c>. The loadings <c>B̂</c> and factors <c>F̂</c> are
/// recovered by PCA on the sample covariance: the top <c>K</c> eigenvectors (scaled by the
/// square root of their eigenvalues) form <c>B̂</c>, and the factor covariance <c>Θ̂_f</c> is
/// the inverse of the diagonal factor-variance matrix (identity by the <c>F̂F̂ᵀ/T = I_K</c>
/// normalization, so the factor precision in loading-scaled coordinates is the identity).
/// </para>
/// <para>
/// The residual covariance <c>W_ε = Σ̂_ε = Σ̂ − B̂B̂ᵀ</c> is fed to a <b>weighted graphical
/// lasso</b> (eq. 3.3 of the reference):
/// <c>Θ̂_ε = argmin_{Θ≻0} tr(W_ε Θ) − log det Θ + λ Σ_{i≠j} d̂_ii d̂_jj |θ_ij|</c>,
/// with off-diagonal penalty weights <c>W[i,j] = σ̂_ε,i · σ̂_ε,j</c> (residual standard
/// deviations) and zero on the diagonal. The penalty <c>λ</c> is selected by the Bayesian
/// Information Criterion (eq. 3.4) over a 10-point log-spaced grid spanning <c>[0.001, 1.0]</c>,
/// delegated to <c>Boutquin.Numerics</c>' graphical-lasso solver.
/// </para>
/// <para>
/// The full precision is reassembled by Sherman–Morrison–Woodbury (eq. 3.5):
/// <c>Θ̂ = Θ̂_ε − Θ̂_ε B̂ [Θ̂_f + B̂ᵀΘ̂_ε B̂]⁻¹ B̂ᵀΘ̂_ε</c>, and the returned covariance is its
/// inverse <c>Σ̂ = Θ̂⁻¹</c>.
/// </para>
/// <para><b>Accuracy contract:</b> the graphical-lasso solution <c>Θ̂_ε</c> is positive-definite
/// by construction, the SMW low-rank update preserves positive-definiteness, and the inverse of
/// a positive-definite matrix is positive-definite, so the returned <c>Σ̂</c> is positive
/// semi-definite (all eigenvalues <c>≥ 0</c>). Θ̂ is consistent in spectral and ℓ₁ norms
/// (Thm 3 of the reference) under the pervasive-factor and sparse-residual-precision
/// assumptions.</para>
/// <para><b>Intended omissions</b> (not built): observable (Fama–French) factors as a PCA
/// alternative; data-driven factor-count selection (<c>K</c> is a fixed constructor parameter,
/// default <c>1</c>); the paper's MWC/MRC portfolio weight formulations (which belong to the
/// construction layer, not the estimator).</para>
/// <para>
/// Reference: Lee &amp; Seregina (2020/2023), "Optimal Portfolio Using Factor Graphical Lasso",
/// arXiv 2011.00435.
/// </para>
/// </remarks>
public sealed class FactorGraphicalLassoEstimator : ICovarianceEstimator
{
    private static readonly NumericsStats.SampleCovarianceEstimator s_sampleCov = new();

    private const int LambdaGridPoints = 10;
    private const double LambdaGridMin = 0.001;
    private const double LambdaGridMax = 1.0;

    private readonly int _numFactors;

    /// <summary>
    /// Initializes a new instance of the <see cref="FactorGraphicalLassoEstimator"/> class.
    /// </summary>
    /// <param name="numFactors">
    /// The number of latent PCA factors <c>K</c> in the factor model. Must be strictly positive
    /// and strictly less than the number of assets (the residual space must be non-empty). The
    /// canonical single-market-factor value is <c>1</c> (the constructor default). Data-driven
    /// factor-count selection is an intended omission for V1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="numFactors"/> is not strictly positive.</exception>
    public FactorGraphicalLassoEstimator(int numFactors = 1)
    {
        if (numFactors < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numFactors), numFactors, "The number of factors K must be strictly positive.");
        }

        _numFactors = numFactors;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when the return series have different lengths, fewer than two observations, or when K is not strictly less than the number of assets.</exception>
    /// <exception cref="CalculationException">Thrown when any asset has zero sample variance (a degenerate, constant series), or when the assembled precision matrix is singular.</exception>
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);

        var n = returns.Length;
        var t = returns[0].Length;

        if (_numFactors >= n)
        {
            throw new ArgumentException(
                $"The number of factors K={_numFactors} must be strictly less than the number of " +
                $"assets N={n}; the residual space would otherwise be empty.",
                nameof(returns));
        }

        // Sample covariance (N-1 divisor, per the repository convention) as the factor-model
        // base. Σ̂ = sample covariance; its diagonal holds the per-asset variances.
        var sampleCov = s_sampleCov.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());

        var sigmaHat = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                sigmaHat[i, j] = (double)sampleCov[i, j];
            }
        }

        for (var k = 0; k < n; k++)
        {
            if (sigmaHat[k, k] <= 0.0)
            {
                throw new CalculationException(
                    $"Asset at index {k} has zero sample variance (constant return series); " +
                    "the Factor Graphical Lasso estimate is undefined for a degenerate asset.");
            }
        }

        // ── PCA factor extraction ────────────────────────────────────────────
        // Eigendecompose the sample covariance. The top-K eigenvector/eigenvalue pairs span the
        // factor (systematic) subspace; B̂ = U_K · sqrt(Λ_K) gives loadings such that B̂B̂ᵀ is the
        // rank-K systematic covariance, and the residual covariance is W_ε = Σ̂ − B̂B̂ᵀ.
        var (eigenvalues, eigenvectors) = JacobiEigen(sigmaHat, n);
        var order = Enumerable.Range(0, n).OrderByDescending(idx => eigenvalues[idx]).ToArray();

        var b = new double[n, _numFactors]; // loadings B̂ (N × K)
        for (var f = 0; f < _numFactors; f++)
        {
            var col = order[f];
            var lambda = Math.Max(eigenvalues[col], 0.0);
            var scale = Math.Sqrt(lambda);
            for (var i = 0; i < n; i++)
            {
                b[i, f] = eigenvectors[i, col] * scale;
            }
        }

        // Residual covariance W_ε = Σ̂ − B̂B̂ᵀ. This is the graphical-lasso input (eq. 3.3 W_ε).
        var residualCov = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var bbt = 0.0;
                for (var f = 0; f < _numFactors; f++)
                {
                    bbt += b[i, f] * b[j, f];
                }

                residualCov[i, j] = sigmaHat[i, j] - bbt;
            }
        }

        // Residual standard deviations d̂_ii — clamp the (possibly slightly negative from the
        // low-rank subtraction) residual variance at a small floor so the penalty weights and
        // the GL solve are well-posed. The low-rank subtraction B̂B̂ᵀ can leave W_ε rank-deficient
        // (the systematic directions are removed); a small diagonal ridge proportional to the
        // mean residual variance keeps W_ε strictly positive-definite so the graphical-lasso
        // solver's internal SPD inversion is well-conditioned. The ridge is O(1e-6) relative to
        // the variance scale and does not perturb the result beyond PrecisionNumeric.
        var meanResidualVar = 0.0;
        for (var i = 0; i < n; i++)
        {
            meanResidualVar += Math.Max(residualCov[i, i], 0.0);
        }

        meanResidualVar /= n;
        var ridge = Math.Max(meanResidualVar * 1e-6, 1e-12);

        var residualStd = new double[n];
        for (var i = 0; i < n; i++)
        {
            var variance = residualCov[i, i];
            if (variance < ridge)
            {
                variance = ridge;
            }
            else
            {
                variance += ridge;
            }

            residualCov[i, i] = variance;
            residualStd[i] = Math.Sqrt(variance);
        }

        // ── Weighted graphical lasso on the residual covariance (eq. 3.3) ────
        // Off-diagonal penalty weights W[i,j] = d̂_ii d̂_jj = σ̂_ε,i · σ̂_ε,j; diagonal unpenalized.
        var penaltyWeights = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                penaltyWeights[i, j] = i == j ? 0.0 : residualStd[i] * residualStd[j];
            }
        }

        // BIC-selected λ over a 10-point log-spaced grid in [0.001, 1.0] (eq. 3.4).
        var lambdaGrid = LogSpace(LambdaGridMin, LambdaGridMax, LambdaGridPoints);
        var glOptions = new NumericsStats.GraphicalLassoOptions<double> { Weights = penaltyWeights };
        double[,] thetaEps; // Θ̂_ε (N × N), positive-definite by construction.
        try
        {
            var glResult = NumericsStats.GraphicalLassoSolver<double>.SolveWithBic(
                residualCov, t, lambdaGrid, glOptions);
            thetaEps = glResult.Precision;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArithmeticException)
        {
            // The residual covariance is too ill-conditioned for the graphical-lasso solver
            // (e.g. a degenerate panel the ridge cannot rescue). Surface as a domain failure.
            throw new CalculationException(
                "Factor Graphical Lasso: the residual graphical-lasso solve failed because the " +
                "residual covariance is singular or ill-conditioned. " + ex.Message);
        }

        // ── Sherman–Morrison–Woodbury reassembly (eq. 3.5) ──────────────────
        // Θ̂ = Θ̂_ε − Θ̂_ε B̂ [Θ̂_f + B̂ᵀΘ̂_ε B̂]⁻¹ B̂ᵀΘ̂_ε, with Θ̂_f = I_K (the factor precision in
        // loading-scaled coordinates, since B̂ already absorbs the eigenvalue scaling and the
        // factor normalization FFᵀ/T = I_K makes the factor covariance the identity).
        var thetaEpsB = MatMul(thetaEps, b, n, n, _numFactors); // Θ̂_ε B̂  (N × K)
        var bt = Transpose(b, n, _numFactors); // B̂ᵀ (K × N)
        var btThetaEps = MatMul(bt, thetaEps, _numFactors, n, n); // B̂ᵀΘ̂_ε (K × N)
        var btThetaEpsB = MatMul(btThetaEps, b, _numFactors, n, _numFactors); // B̂ᵀΘ̂_ε B̂ (K × K)

        // Inner K×K matrix M = Θ̂_f + B̂ᵀΘ̂_ε B̂ = I_K + B̂ᵀΘ̂_ε B̂.
        var inner = new double[_numFactors, _numFactors];
        for (var i = 0; i < _numFactors; i++)
        {
            for (var j = 0; j < _numFactors; j++)
            {
                inner[i, j] = btThetaEpsB[i, j] + (i == j ? 1.0 : 0.0);
            }
        }

        var innerInv = InvertSmall(inner, _numFactors);

        // correction = Θ̂_ε B̂ · innerInv · B̂ᵀΘ̂_ε   (N × N)
        var tmp = MatMul(thetaEpsB, innerInv, n, _numFactors, _numFactors); // (N × K)
        var correction = MatMul(tmp, btThetaEps, n, _numFactors, n); // (N × N)

        var theta = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                theta[i, j] = thetaEps[i, j] - correction[i, j];
            }
        }

        // Symmetrize to scrub asymmetric numerical dust before inversion.
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var avg = 0.5 * (theta[i, j] + theta[j, i]);
                theta[i, j] = avg;
                theta[j, i] = avg;
            }
        }

        // Σ̂ = Θ̂⁻¹.
        var covDouble = InvertSpd(theta, n);

        var cov = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                var avg = 0.5 * (covDouble[i, j] + covDouble[j, i]);
                var value = (decimal)avg;
                cov[i, j] = value;
                cov[j, i] = value;
            }
        }

        return cov;
    }

    // ─── Linear-algebra helpers (double precision) ──────────────────────────

    private static double[] LogSpace(double min, double max, int points)
    {
        var grid = new double[points];
        var logMin = Math.Log(min);
        var logMax = Math.Log(max);
        for (var i = 0; i < points; i++)
        {
            var frac = points == 1 ? 0.0 : (double)i / (points - 1);
            grid[i] = Math.Exp(logMin + (frac * (logMax - logMin)));
        }

        return grid;
    }

    private static double[,] MatMul(double[,] a, double[,] b, int rows, int inner, int cols)
    {
        var result = new double[rows, cols];
        for (var i = 0; i < rows; i++)
        {
            for (var k = 0; k < inner; k++)
            {
                var aik = a[i, k];
                if (aik == 0.0)
                {
                    continue;
                }

                for (var j = 0; j < cols; j++)
                {
                    result[i, j] += aik * b[k, j];
                }
            }
        }

        return result;
    }

    private static double[,] Transpose(double[,] a, int rows, int cols)
    {
        var result = new double[cols, rows];
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                result[j, i] = a[i, j];
            }
        }

        return result;
    }

    /// <summary>
    /// Inverts a small dense matrix by Gauss–Jordan elimination with partial pivoting.
    /// Used for the K×K SMW inner matrix.
    /// </summary>
    private static double[,] InvertSmall(double[,] matrix, int n)
    {
        var a = (double[,])matrix.Clone();
        var inv = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            inv[i, i] = 1.0;
        }

        for (var col = 0; col < n; col++)
        {
            var pivotRow = col;
            var maxAbs = Math.Abs(a[col, col]);
            for (var r = col + 1; r < n; r++)
            {
                var abs = Math.Abs(a[r, col]);
                if (abs > maxAbs)
                {
                    maxAbs = abs;
                    pivotRow = r;
                }
            }

            if (maxAbs < 1e-300)
            {
                throw new CalculationException(
                    "Factor Graphical Lasso: the Sherman–Morrison–Woodbury inner matrix is " +
                    "singular; cannot reassemble the full precision.");
            }

            if (pivotRow != col)
            {
                SwapRows(a, col, pivotRow, n);
                SwapRows(inv, col, pivotRow, n);
            }

            var pivot = a[col, col];
            for (var j = 0; j < n; j++)
            {
                a[col, j] /= pivot;
                inv[col, j] /= pivot;
            }

            for (var r = 0; r < n; r++)
            {
                if (r == col)
                {
                    continue;
                }

                var factor = a[r, col];
                if (factor == 0.0)
                {
                    continue;
                }

                for (var j = 0; j < n; j++)
                {
                    a[r, j] -= factor * a[col, j];
                    inv[r, j] -= factor * inv[col, j];
                }
            }
        }

        return inv;
    }

    /// <summary>
    /// Inverts a symmetric positive-definite matrix. Uses Gauss–Jordan with partial pivoting
    /// (robust for the small N this estimator targets); throws a <see cref="CalculationException"/>
    /// if the matrix is singular.
    /// </summary>
    private static double[,] InvertSpd(double[,] matrix, int n)
    {
        try
        {
            return InvertSmall(matrix, n);
        }
        catch (CalculationException)
        {
            throw new CalculationException(
                "Factor Graphical Lasso: the assembled precision matrix Θ̂ is singular and cannot " +
                "be inverted to a covariance.");
        }
    }

    private static void SwapRows(double[,] m, int r1, int r2, int n)
    {
        for (var j = 0; j < n; j++)
        {
            (m[r1, j], m[r2, j]) = (m[r2, j], m[r1, j]);
        }
    }

    /// <summary>
    /// Jacobi eigenvalue/eigenvector decomposition of a symmetric matrix. Returns eigenvalues
    /// and a column-eigenvector matrix (column <c>c</c> is the eigenvector for
    /// <c>eigenvalues[c]</c>). Matches the Jacobi-based eigensolver used elsewhere in the
    /// covariance-estimator family (e.g. <c>DenoisedCovarianceEstimator</c>).
    /// </summary>
    private static (double[] Eigenvalues, double[,] Eigenvectors) JacobiEigen(double[,] matrix, int n)
    {
        var a = (double[,])matrix.Clone();
        var v = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            v[i, i] = 1.0;
        }

        for (var sweep = 0; sweep < 100; sweep++)
        {
            var off = 0.0;
            for (var p = 0; p < n; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    off += a[p, q] * a[p, q];
                }
            }

            if (off < 1e-30)
            {
                break;
            }

            for (var p = 0; p < n; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-300)
                    {
                        continue;
                    }

                    var theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                    var sign = theta >= 0.0 ? 1.0 : -1.0;
                    var tee = sign / (Math.Abs(theta) + Math.Sqrt((theta * theta) + 1.0));
                    var cos = 1.0 / Math.Sqrt((tee * tee) + 1.0);
                    var sin = tee * cos;

                    for (var i = 0; i < n; i++)
                    {
                        var aip = a[i, p];
                        var aiq = a[i, q];
                        a[i, p] = (cos * aip) - (sin * aiq);
                        a[i, q] = (sin * aip) + (cos * aiq);
                    }

                    for (var i = 0; i < n; i++)
                    {
                        var api = a[p, i];
                        var aqi = a[q, i];
                        a[p, i] = (cos * api) - (sin * aqi);
                        a[q, i] = (sin * api) + (cos * aqi);
                    }

                    for (var i = 0; i < n; i++)
                    {
                        var vip = v[i, p];
                        var viq = v[i, q];
                        v[i, p] = (cos * vip) - (sin * viq);
                        v[i, q] = (sin * vip) + (cos * viq);
                    }
                }
            }
        }

        var eigenvalues = new double[n];
        for (var i = 0; i < n; i++)
        {
            eigenvalues[i] = a[i, i];
        }

        return (eigenvalues, v);
    }
}
