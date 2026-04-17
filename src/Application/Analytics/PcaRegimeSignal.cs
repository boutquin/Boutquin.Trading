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

using Boutquin.Numerics.LinearAlgebra;
using CovarianceEstimators;

/// <summary>
/// PCA-based regime detection signals derived from the eigenvalue spectrum
/// and eigenvector stability of the correlation matrix.
///
/// Two complementary signals:
///
/// 1. PC1 Variance Share — the fraction of total variance explained by the first
///    principal component. Spikes during crises when correlations converge to 1.
///    Normal: 50-70%, Stress: 70-85%, Crisis: >85%.
///
/// 2. Eigenvector Stability — cosine similarity between eigenvectors at two points
///    in time. Drops when the correlation structure undergoes a regime shift
///    (e.g., the 2022 stocks-bonds correlation flip).
///
/// Reference: Kritzman, M., Li, Y., Page, S., and Rigobon, R. (2011).
/// "Principal Components as a Measure of Systemic Risk." The Journal of Portfolio Management.
/// </summary>
public static class PcaRegimeSignal
{
    /// <summary>
    /// Result of variance share analysis.
    /// </summary>
    /// <param name="Pc1VarianceShare">Fraction of total variance explained by PC1 (0 to 1).</param>
    /// <param name="AllVarianceShares">Variance shares for all PCs, descending order.</param>
    /// <param name="Eigenvalues">Raw eigenvalues, descending order.</param>
    public sealed record VarianceShareResult(
        decimal Pc1VarianceShare,
        decimal[] AllVarianceShares,
        decimal[] Eigenvalues);

    /// <summary>
    /// Result of eigenvector stability analysis.
    /// </summary>
    /// <param name="Pc1CosineSimilarity">Absolute cosine similarity of PC1 eigenvectors (0 to 1).</param>
    /// <param name="AllCosineSimilarities">Cosine similarities for all PCs, in eigenvalue-descending order.</param>
    public sealed record EigenvectorStabilityResult(
        decimal Pc1CosineSimilarity,
        decimal[] AllCosineSimilarities);

    /// <summary>
    /// Computes the variance share of each principal component from a correlation matrix.
    /// </summary>
    /// <param name="correlationMatrix">NxN symmetric correlation matrix with unit diagonal.</param>
    /// <returns>Variance share result with PC1 share and all shares.</returns>
    public static VarianceShareResult ComputeVarianceShare(decimal[,] correlationMatrix)
    {
        Guard.AgainstNull(() => correlationMatrix);

        var n = correlationMatrix.GetLength(0);
        if (n == 0 || correlationMatrix.GetLength(1) != n)
        {
            throw new ArgumentException("Correlation matrix must be square and non-empty.",
                nameof(correlationMatrix));
        }

        if (n == 1)
        {
            return new VarianceShareResult(1m, [1m], [1m]);
        }

        var eigenvalues = JacobiEigenDecomposition.Decompose(correlationMatrix).Values;

        // Clamp negatives and compute shares
        var total = 0m;
        for (var i = 0; i < n; i++)
        {
            eigenvalues[i] = Math.Max(0m, eigenvalues[i]);
            total += eigenvalues[i];
        }

        var shares = new decimal[n];
        if (total > 0m)
        {
            for (var i = 0; i < n; i++)
            {
                shares[i] = eigenvalues[i] / total;
            }
        }
        else
        {
            // Degenerate: all zero eigenvalues
            shares[0] = 1m;
        }

        return new VarianceShareResult(shares[0], shares, eigenvalues);
    }

    /// <summary>
    /// Computes the variance share from return series (computes sample correlation internally).
    /// </summary>
    public static VarianceShareResult ComputeVarianceShareFromReturns(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);

        var n = returns.Length;
        if (n == 1)
        {
            return new VarianceShareResult(1m, [1m], [1m]);
        }

        var sampleCov = new SampleCovarianceEstimator().Estimate(returns);
        var corr = CovarianceToCorrelation(sampleCov, n);

        return ComputeVarianceShare(corr);
    }

    /// <summary>
    /// Computes the eigenvector cosine similarity between two correlation matrices.
    /// Measures structural stability of the factor decomposition over time.
    /// </summary>
    /// <param name="corrA">Correlation matrix at time t-1.</param>
    /// <param name="corrB">Correlation matrix at time t.</param>
    /// <returns>Stability result with cosine similarities for each PC.</returns>
    public static EigenvectorStabilityResult ComputeEigenvectorStability(
        decimal[,] corrA, decimal[,] corrB)
    {
        Guard.AgainstNull(() => corrA);
        Guard.AgainstNull(() => corrB);

        var nA = corrA.GetLength(0);
        var nB = corrB.GetLength(0);

        if (nA != nB || nA == 0)
        {
            throw new ArgumentException("Both correlation matrices must be square, non-empty, and same size.");
        }

        var eigenA = JacobiEigenDecomposition.Decompose(corrA);
        var eigenB = JacobiEigenDecomposition.Decompose(corrB);

        var similarities = new decimal[nA];
        for (var k = 0; k < nA; k++)
        {
            // Extract k-th eigenvector from each decomposition
            var dotProduct = 0m;
            var normA = 0m;
            var normB = 0m;

            for (var i = 0; i < nA; i++)
            {
                var a = eigenA.Vectors[i, k];
                var b = eigenB.Vectors[i, k];
                dotProduct += a * b;
                normA += a * a;
                normB += b * b;
            }

            if (normA > 0m && normB > 0m)
            {
                var cosine = dotProduct / ((decimal)Math.Sqrt((double)normA) * (decimal)Math.Sqrt((double)normB));
                // Use absolute value — eigenvectors can have arbitrary sign
                similarities[k] = Math.Abs(cosine);
            }
            else
            {
                similarities[k] = 0m;
            }
        }

        return new EigenvectorStabilityResult(similarities[0], similarities);
    }

    private static decimal[,] CovarianceToCorrelation(decimal[,] cov, int n)
    {
        var stdDevs = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            stdDevs[i] = (decimal)Math.Sqrt((double)cov[i, i]);
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

        return corr;
    }
}
