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
/// Computes principal portfolios — the investable portfolios corresponding to each
/// independent source of risk (principal component) in the correlation structure.
///
/// Each principal portfolio captures exactly one eigenvalue of risk. Together they
/// decompose the portfolio's total risk into orthogonal, interpretable components.
///
/// The weights are normalized so each principal portfolio is investable (weights sum to 1).
/// Risk contributions are computed as the fraction of total portfolio variance attributable
/// to each principal component.
///
/// Reference: Meucci, A. (2009). "Managing Diversification." Risk Magazine.
/// Partovi, M.H. and Caputo, M. (2004). "Principal Portfolios." Journal of Economics and Finance.
/// </summary>
public static class PrincipalPortfolioAnalyzer
{
    /// <summary>
    /// Represents a single principal portfolio with its weights and risk contribution.
    /// </summary>
    /// <param name="ComponentIndex">Zero-based index of the principal component (0 = PC1, dominant).</param>
    /// <param name="Weights">Symbol weights for this principal portfolio (sum to 1).</param>
    /// <param name="Eigenvalue">The eigenvalue associated with this principal component.</param>
    /// <param name="RiskContribution">Fraction of total risk attributable to this PC (sums to 1 across all PCs).</param>
    public sealed record PrincipalPortfolio(
        int ComponentIndex,
        decimal[] Weights,
        decimal Eigenvalue,
        decimal RiskContribution);

    /// <summary>
    /// Result of principal portfolio analysis.
    /// </summary>
    /// <param name="Portfolios">Principal portfolios, ordered by eigenvalue (descending).</param>
    public sealed record PrincipalPortfolioResult(IReadOnlyList<PrincipalPortfolio> Portfolios);

    /// <summary>
    /// Computes principal portfolios from a correlation matrix and asset variances.
    /// </summary>
    /// <param name="correlationMatrix">NxN symmetric correlation matrix with unit diagonal.</param>
    /// <param name="variances">Per-asset variances (length N).</param>
    /// <returns>Principal portfolio decomposition.</returns>
    public static PrincipalPortfolioResult Analyze(decimal[,] correlationMatrix, decimal[] variances)
    {
        Guard.AgainstNull(() => correlationMatrix);
        Guard.AgainstNull(() => variances);

        var n = correlationMatrix.GetLength(0);
        if (n == 0 || correlationMatrix.GetLength(1) != n)
        {
            throw new ArgumentException("Correlation matrix must be square and non-empty.",
                nameof(correlationMatrix));
        }

        if (variances.Length != n)
        {
            throw new ArgumentException(
                $"Variances length ({variances.Length}) must match correlation matrix dimension ({n}).",
                nameof(variances));
        }

        if (n == 1)
        {
            return new PrincipalPortfolioResult(
                [new PrincipalPortfolio(0, [1m], correlationMatrix[0, 0], 1m)]);
        }

        // Eigendecompose the correlation matrix
        var eigen = JacobiEigenDecomposition.Decompose(correlationMatrix);
        var eigenvalues = eigen.Values;
        var eigenvectors = eigen.Vectors;

        // Compute total eigenvalue (for risk contribution normalization)
        var totalEigenvalue = 0m;
        for (var i = 0; i < n; i++)
        {
            eigenvalues[i] = Math.Max(0m, eigenvalues[i]);
            totalEigenvalue += eigenvalues[i];
        }

        if (totalEigenvalue <= 0m)
        {
            totalEigenvalue = 1m; // prevent division by zero
        }

        var portfolios = new PrincipalPortfolio[n];

        for (var k = 0; k < n; k++)
        {
            // Extract k-th eigenvector
            var rawWeights = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                rawWeights[i] = eigenvectors[i, k];
            }

            // Normalize sign convention: largest absolute loading should be positive
            var maxAbsIdx = 0;
            var maxAbs = Math.Abs(rawWeights[0]);
            for (var i = 1; i < n; i++)
            {
                if (Math.Abs(rawWeights[i]) > maxAbs)
                {
                    maxAbs = Math.Abs(rawWeights[i]);
                    maxAbsIdx = i;
                }
            }

            if (rawWeights[maxAbsIdx] < 0m)
            {
                for (var i = 0; i < n; i++)
                {
                    rawWeights[i] = -rawWeights[i];
                }
            }

            // Normalize to sum to 1 (investable portfolio)
            var weightSum = rawWeights.Sum();
            var weights = new decimal[n];

            if (Math.Abs(weightSum) > 1e-20m)
            {
                for (var i = 0; i < n; i++)
                {
                    weights[i] = rawWeights[i] / weightSum;
                }
            }
            else
            {
                // Degenerate eigenvector (sums to zero) — use absolute normalization
                var absSum = rawWeights.Sum(Math.Abs);
                if (absSum > 0m)
                {
                    for (var i = 0; i < n; i++)
                    {
                        weights[i] = Math.Abs(rawWeights[i]) / absSum;
                    }
                }
                else
                {
                    // All zeros — equal weight
                    for (var i = 0; i < n; i++)
                    {
                        weights[i] = 1m / n;
                    }
                }
            }

            var riskContribution = eigenvalues[k] / totalEigenvalue;

            portfolios[k] = new PrincipalPortfolio(k, weights, eigenvalues[k], riskContribution);
        }

        return new PrincipalPortfolioResult(portfolios);
    }

    /// <summary>
    /// Computes principal portfolios from return series.
    /// </summary>
    public static PrincipalPortfolioResult AnalyzeFromReturns(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);

        var n = returns.Length;
        if (n == 1)
        {
            return new PrincipalPortfolioResult(
                [new PrincipalPortfolio(0, [1m], 1m, 1m)]);
        }

        var sampleCov = new SampleCovarianceEstimator().Estimate(returns);

        // Extract variances and correlation matrix
        var variances = new decimal[n];
        var stdDevs = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            variances[i] = sampleCov[i, i];
            stdDevs[i] = DecimalSqrt(variances[i]);
        }

        var corr = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (stdDevs[i] == 0m || stdDevs[j] == 0m)
                {
                    corr[i, j] = i == j ? 1m : 0m;
                }
                else
                {
                    corr[i, j] = sampleCov[i, j] / (stdDevs[i] * stdDevs[j]);
                }
            }
        }

        return Analyze(corr, variances);
    }

    private static decimal DecimalSqrt(decimal value)
    {
        if (value <= 0m)
        {
            return 0m;
        }

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
