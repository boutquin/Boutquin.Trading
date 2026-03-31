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

namespace Boutquin.Trading.Application.CovarianceEstimators;

/// <summary>
/// Denoised covariance estimator using Random Matrix Theory (Marcenko-Pastur distribution).
/// Identifies noise eigenvalues in the sample covariance matrix and replaces them with their
/// average, preserving total variance (trace) while removing estimation noise.
///
/// Based on Lopez de Prado (2018), "Advances in Financial Machine Learning", Chapter 2.
///
/// Optionally composes with Ledoit-Wolf shrinkage for additional regularization.
/// </summary>
public sealed class DenoisedCovarianceEstimator : ICovarianceEstimator
{
    private static readonly SampleCovarianceEstimator s_sampleEstimator = new();
    private readonly bool _applyLedoitWolfShrinkage;

    /// <summary>
    /// Initializes a new instance of the <see cref="DenoisedCovarianceEstimator"/> class.
    /// </summary>
    /// <param name="applyLedoitWolfShrinkage">
    /// When true, applies Ledoit-Wolf shrinkage after denoising for additional regularization.
    /// Default is false (pure denoising).
    /// </param>
    public DenoisedCovarianceEstimator(bool applyLedoitWolfShrinkage = false)
    {
        _applyLedoitWolfShrinkage = applyLedoitWolfShrinkage;
    }

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);

        var n = returns.Length;       // Number of assets
        var t = returns[0].Length;    // Number of observations

        // Step 1: Compute sample covariance matrix
        var sampleCov = s_sampleEstimator.Estimate(returns);

        // For very small N (< 3), denoising is not meaningful — return sample covariance
        if (n < 3)
        {
            return _applyLedoitWolfShrinkage
                ? new LedoitWolfShrinkageEstimator().Estimate(returns)
                : sampleCov;
        }

        // Step 2: Convert to correlation matrix for eigendecomposition
        var (corrMatrix, stdDevs) = CovarianceToCorrelation(sampleCov, n);

        // Step 3: Eigendecomposition via Jacobi iteration
        var (eigenvalues, eigenvectors) = EigenDecompose(corrMatrix, n);

        // Step 4: Compute Marcenko-Pastur upper bound
        var q = (decimal)t / n;  // observations-to-assets ratio
        var mpUpperBound = MarcenkoPasturUpperBound(q);

        // Step 5: Separate signal and noise eigenvalues
        var noiseCount = 0;
        var noiseSum = 0m;
        for (var i = 0; i < n; i++)
        {
            if (eigenvalues[i] <= mpUpperBound)
            {
                noiseCount++;
                noiseSum += eigenvalues[i];
            }
        }

        // Step 6: Replace noise eigenvalues with their average (preserves trace)
        if (noiseCount > 0 && noiseCount < n)
        {
            var noiseAvg = noiseSum / noiseCount;
            for (var i = 0; i < n; i++)
            {
                if (eigenvalues[i] <= mpUpperBound)
                {
                    eigenvalues[i] = noiseAvg;
                }
            }
        }

        // Step 7: Reconstruct correlation matrix from cleaned eigenvalues
        var cleanedCorr = ReconstructFromEigen(eigenvalues, eigenvectors, n);

        // Step 8: Force unit diagonal (numerical drift from reconstruction)
        for (var i = 0; i < n; i++)
        {
            cleanedCorr[i, i] = 1.0m;
        }

        // Step 9: Convert back to covariance matrix
        var result = CorrelationToCovariance(cleanedCorr, stdDevs, n);

        // Step 10: Optionally apply Ledoit-Wolf shrinkage on top
        if (_applyLedoitWolfShrinkage)
        {
            result = ApplyShrinkageToCovariance(result, n);
        }

        return result;
    }

    /// <summary>
    /// Converts a covariance matrix to a correlation matrix and extracts standard deviations.
    /// </summary>
    private static (decimal[,] Correlation, decimal[] StdDevs) CovarianceToCorrelation(
        decimal[,] cov, int n)
    {
        var stdDevs = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            stdDevs[i] = DecimalSqrt(cov[i, i]);
        }

        var corr = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (stdDevs[i] == 0m || stdDevs[j] == 0m)
                {
                    corr[i, j] = i == j ? 1.0m : 0.0m;
                }
                else
                {
                    corr[i, j] = cov[i, j] / (stdDevs[i] * stdDevs[j]);
                }
            }
        }

        return (corr, stdDevs);
    }

    /// <summary>
    /// Converts a correlation matrix back to a covariance matrix using standard deviations.
    /// </summary>
    private static decimal[,] CorrelationToCovariance(decimal[,] corr, decimal[] stdDevs, int n)
    {
        var cov = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                cov[i, j] = corr[i, j] * stdDevs[i] * stdDevs[j];
            }
        }

        return cov;
    }

    /// <summary>
    /// Computes the Marcenko-Pastur upper bound for noise eigenvalues.
    /// lambda_+ = (1 + 1/sqrt(q))^2 where q = T/N.
    /// </summary>
    private static decimal MarcenkoPasturUpperBound(decimal q)
    {
        // lambda_+ = (1 + 1/sqrt(q))^2
        var sqrtQ = DecimalSqrt(q);
        if (sqrtQ == 0m)
        {
            return decimal.MaxValue;
        }

        var bound = 1m + 1m / sqrtQ;
        return bound * bound;
    }

    /// <summary>
    /// Reconstructs a symmetric matrix from eigenvalues and eigenvectors: A = V * diag(lambda) * V^T.
    /// </summary>
    private static decimal[,] ReconstructFromEigen(decimal[] eigenvalues, decimal[,] eigenvectors, int n)
    {
        var result = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                var sum = 0m;
                for (var k = 0; k < n; k++)
                {
                    sum += eigenvalues[k] * eigenvectors[i, k] * eigenvectors[j, k];
                }

                result[i, j] = sum;
                result[j, i] = sum;
            }
        }

        return result;
    }

    /// <summary>
    /// Applies Ledoit-Wolf-style shrinkage to an already-denoised covariance matrix.
    /// Shrinks toward scaled identity with shrinkage intensity based on Frobenius norm.
    /// </summary>
    private static decimal[,] ApplyShrinkageToCovariance(decimal[,] cov, int n)
    {
        // Target: mu * I, where mu = average diagonal
        var mu = 0m;
        for (var i = 0; i < n; i++)
        {
            mu += cov[i, i];
        }

        mu /= n;

        // Use a conservative fixed shrinkage intensity for post-denoised matrices.
        // The denoising already handled the noise — this is a light smoothing pass.
        const decimal shrinkageIntensity = 0.1m;

        var result = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var target = i == j ? mu : 0m;
                result[i, j] = shrinkageIntensity * target + (1m - shrinkageIntensity) * cov[i, j];
            }
        }

        return result;
    }

    /// <summary>
    /// Eigendecomposition of a symmetric matrix using Jacobi iteration.
    /// Uses double internally to avoid decimal overflow on intermediate products,
    /// converts back to decimal for the final result.
    /// Returns eigenvalues (descending) and eigenvectors (columns).
    /// </summary>
    private static (decimal[] Eigenvalues, decimal[,] Eigenvectors) EigenDecompose(
        decimal[,] matrix, int n)
    {
        // Convert to double for numerical stability in Jacobi rotations
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
                        RotateDouble(a, r, p, r, q, s, tau);
                    }

                    for (var r = p + 1; r < q; r++)
                    {
                        RotateDouble(a, p, r, r, q, s, tau);
                    }

                    for (var r = q + 1; r < n; r++)
                    {
                        RotateDouble(a, p, r, q, r, s, tau);
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

        // Convert results back to decimal
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

    private static void RotateDouble(double[,] a, int i1, int j1, int i2, int j2, double s, double tau)
    {
        var g1 = a[i1, j1];
        var g2 = a[i2, j2];
        a[i1, j1] = g1 - s * (g2 + tau * g1);
        a[i2, j2] = g2 + s * (g1 - tau * g2);
    }

    /// <summary>
    /// Computes the square root of a decimal value using Newton's method.
    /// </summary>
    private static decimal DecimalSqrt(decimal value)
    {
        if (value < 0m)
        {
            throw new ArgumentException("Cannot compute square root of a negative number.", nameof(value));
        }

        if (value == 0m)
        {
            return 0m;
        }

        // Newton's method with decimal precision
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
