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

        var eigenvalues = JacobiEigenDecomposition.Decompose(correlationMatrix).Values;
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
        // Clamp negative eigenvalues to zero (numerical artifact) — use local copy to avoid mutating caller's array
        var clamped = (decimal[])eigenvalues.Clone();
        var totalEigenvalue = 0m;
        for (var i = 0; i < clamped.Length; i++)
        {
            clamped[i] = Math.Max(0m, clamped[i]);
            totalEigenvalue += clamped[i];
        }

        if (totalEigenvalue <= 0m)
        {
            return 1m;
        }

        // Compute entropy: H = -sum(p_i * ln(p_i))
        var entropy = 0m;
        for (var i = 0; i < clamped.Length; i++)
        {
            var p = clamped[i] / totalEigenvalue;
            if (p > 0m)
            {
                entropy -= p * (decimal)Math.Log((double)p);
            }
        }

        // ENB = exp(H)
        return (decimal)Math.Exp((double)entropy);
    }
}
