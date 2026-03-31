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

namespace Boutquin.Trading.Application.Analytics;

using CovarianceEstimators;

/// <summary>
/// Computes the Effective Number of Bets (ENB) — an entropy-based measure of portfolio
/// diversification derived from the eigenvalue spectrum of the correlation matrix.
///
/// ENB = exp(-sum(p_i * ln(p_i))) where p_i = lambda_i / sum(lambda)
///
/// A portfolio with N assets has ENB = N when all eigenvalues are equal (perfect diversification).
/// When correlations are high, ENB drops toward 1 (effectively one bet).
///
/// Reference: Meucci, A. (2009). "Managing Diversification."
/// </summary>
public static class EffectiveNumberOfBets
{
    /// <summary>
    /// Computes ENB from a correlation matrix.
    /// </summary>
    /// <param name="correlationMatrix">NxN symmetric correlation matrix with unit diagonal.</param>
    /// <returns>Effective number of bets (1.0 to N).</returns>
    /// <exception cref="ArgumentException">If matrix is null, empty, or non-square.</exception>
    public static decimal Compute(decimal[,] correlationMatrix)
    {
        Guard.AgainstNull(() => correlationMatrix);

        var n = correlationMatrix.GetLength(0);
        if (n == 0 || correlationMatrix.GetLength(1) != n)
        {
            throw new ArgumentException("Correlation matrix must be square and non-empty.", nameof(correlationMatrix));
        }

        if (n == 1)
        {
            return 1m;
        }

        // Eigendecompose the correlation matrix
        var eigenvalues = ComputeEigenvalues(correlationMatrix, n);

        return ComputeFromEigenvalues(eigenvalues);
    }

    /// <summary>
    /// Computes ENB from a set of return series by first estimating the correlation matrix.
    /// </summary>
    /// <param name="returns">Jagged array where returns[i] is the return series for asset i.</param>
    /// <returns>Effective number of bets (1.0 to N).</returns>
    public static decimal ComputeFromReturns(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);

        var n = returns.Length;
        if (n == 1)
        {
            return 1m;
        }

        // Compute sample covariance
        var sampleCov = new SampleCovarianceEstimator().Estimate(returns);

        // Convert to correlation matrix
        var corr = new decimal[n, n];
        var stdDevs = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            stdDevs[i] = (decimal)Math.Sqrt((double)sampleCov[i, i]);
        }

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                corr[i, j] = (stdDevs[i] == 0m || stdDevs[j] == 0m)
                    ? (i == j ? 1m : 0m)
                    : sampleCov[i, j] / (stdDevs[i] * stdDevs[j]);
            }
        }

        return Compute(corr);
    }

    /// <summary>
    /// Computes ENB directly from eigenvalues.
    /// </summary>
    internal static decimal ComputeFromEigenvalues(decimal[] eigenvalues)
    {
        // Clamp negative eigenvalues to zero (numerical artifact)
        var totalEigenvalue = 0m;
        for (var i = 0; i < eigenvalues.Length; i++)
        {
            eigenvalues[i] = Math.Max(0m, eigenvalues[i]);
            totalEigenvalue += eigenvalues[i];
        }

        if (totalEigenvalue <= 0m)
        {
            return 1m;
        }

        // Compute entropy: H = -sum(p_i * ln(p_i))
        var entropy = 0m;
        for (var i = 0; i < eigenvalues.Length; i++)
        {
            var p = eigenvalues[i] / totalEigenvalue;
            if (p > 0m)
            {
                entropy -= p * (decimal)Math.Log((double)p);
            }
        }

        // ENB = exp(H)
        return (decimal)Math.Exp((double)entropy);
    }

    /// <summary>
    /// Extracts eigenvalues from a symmetric matrix using Jacobi iteration.
    /// Uses double internally to avoid decimal overflow.
    /// Returns eigenvalues in descending order.
    /// </summary>
    private static decimal[] ComputeEigenvalues(decimal[,] matrix, int n)
    {
        var a = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                a[i, j] = (double)matrix[i, j];
            }
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
                }
            }
        }

        var eigenvalues = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            eigenvalues[i] = (decimal)a[i, i];
        }

        Array.Sort(eigenvalues, (x, y) => y.CompareTo(x)); // descending
        return eigenvalues;
    }

    private static void RotateDouble(double[,] a, int i1, int j1, int i2, int j2, double s, double tau)
    {
        var g1 = a[i1, j1];
        var g2 = a[i2, j2];
        a[i1, j1] = g1 - s * (g2 + tau * g1);
        a[i2, j2] = g2 + s * (g1 - tau * g2);
    }

}
