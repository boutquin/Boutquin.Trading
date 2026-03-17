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

using Boutquin.Trading.Domain.Analytics;
using Boutquin.Trading.Domain.Exceptions;

namespace Boutquin.Trading.Application.Analytics;

/// <summary>
/// Performs multi-factor ordinary least squares (OLS) regression of portfolio returns
/// against risk factor returns (e.g., Fama-French factors).
/// </summary>
/// <remarks>
/// Solves: R_p = alpha + Σ(beta_i * F_i) + epsilon
/// using the normal equations: (X'X)^(-1) X'y
/// </remarks>
public static class FactorRegressor
{
    /// <summary>
    /// Regresses portfolio excess returns against a set of factor returns.
    /// </summary>
    /// <param name="portfolioReturns">Array of T portfolio returns.</param>
    /// <param name="factorNames">Names of the K factors.</param>
    /// <param name="factorReturns">K arrays of T factor returns each.</param>
    /// <returns>A <see cref="FactorRegressionResult"/> with alpha, betas, R², and residual standard error.</returns>
    public static FactorRegressionResult Regress(
        decimal[] portfolioReturns,
        IReadOnlyList<string> factorNames,
        decimal[][] factorReturns)
    {
        var t = portfolioReturns.Length;
        var k = factorNames.Count;

        if (t < k + 2)
        {
            throw new ArgumentException(
                $"Need at least {k + 2} observations for {k} factors plus intercept, but got {t}.",
                nameof(portfolioReturns));
        }

        foreach (var fr in factorReturns)
        {
            if (fr.Length != t)
            {
                throw new ArgumentException(
                    $"Factor return array length {fr.Length} does not match portfolio return length {t}.",
                    nameof(factorReturns));
            }
        }

        // Build X matrix: T × (K+1), first column is intercept (1)
        var cols = k + 1;
        var x = new double[t, cols];
        var y = new double[t];

        for (var i = 0; i < t; i++)
        {
            x[i, 0] = 1.0; // Intercept
            for (var j = 0; j < k; j++)
            {
                x[i, j + 1] = (double)factorReturns[j][i];
            }

            y[i] = (double)portfolioReturns[i];
        }

        // Compute X'X (cols × cols)
        var xtx = new double[cols, cols];
        for (var i = 0; i < cols; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                var sum = 0.0;
                for (var r = 0; r < t; r++)
                {
                    sum += x[r, i] * x[r, j];
                }

                xtx[i, j] = sum;
            }
        }

        // Compute X'y (cols × 1)
        var xty = new double[cols];
        for (var i = 0; i < cols; i++)
        {
            var sum = 0.0;
            for (var r = 0; r < t; r++)
            {
                sum += x[r, i] * y[r];
            }

            xty[i] = sum;
        }

        // Solve (X'X) beta = X'y via Gaussian elimination
        var beta = SolveLinearSystem(xtx, xty, cols);

        // Compute predictions and residuals
        var yMean = 0.0;
        for (var i = 0; i < t; i++)
        {
            yMean += y[i];
        }

        yMean /= t;

        var ssTotal = 0.0;
        var ssResidual = 0.0;

        for (var i = 0; i < t; i++)
        {
            var predicted = 0.0;
            for (var j = 0; j < cols; j++)
            {
                predicted += x[i, j] * beta[j];
            }

            var residual = y[i] - predicted;
            ssResidual += residual * residual;
            ssTotal += (y[i] - yMean) * (y[i] - yMean);
        }

        var rSquared = ssTotal > 0 ? 1.0 - ssResidual / ssTotal : 0.0;
        rSquared = Math.Max(0.0, Math.Min(1.0, rSquared));

        var degreesOfFreedom = t - cols;
        var residualStdError = degreesOfFreedom > 0 ? Math.Sqrt(ssResidual / degreesOfFreedom) : 0.0;

        var factorLoadings = new Dictionary<string, decimal>();
        for (var j = 0; j < k; j++)
        {
            factorLoadings[factorNames[j]] = (decimal)beta[j + 1];
        }

        return new FactorRegressionResult(
            Alpha: (decimal)beta[0],
            FactorLoadings: factorLoadings,
            RSquared: (decimal)rSquared,
            ResidualStandardError: (decimal)residualStdError);
    }

    /// <summary>
    /// Solves a linear system Ax = b using Gaussian elimination with partial pivoting.
    /// </summary>
    private static double[] SolveLinearSystem(double[,] a, double[] b, int n)
    {
        // Create augmented matrix
        var aug = new double[n, n + 1];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                aug[i, j] = a[i, j];
            }

            aug[i, n] = b[i];
        }

        // Forward elimination with partial pivoting
        for (var col = 0; col < n; col++)
        {
            // Find pivot
            var maxRow = col;
            var maxVal = Math.Abs(aug[col, col]);
            for (var row = col + 1; row < n; row++)
            {
                if (Math.Abs(aug[row, col]) > maxVal)
                {
                    maxVal = Math.Abs(aug[row, col]);
                    maxRow = row;
                }
            }

            // Swap rows
            if (maxRow != col)
            {
                for (var j = col; j <= n; j++)
                {
                    (aug[col, j], aug[maxRow, j]) = (aug[maxRow, j], aug[col, j]);
                }
            }

            if (Math.Abs(aug[col, col]) < 1e-14)
            {
                throw new CalculationException(
                    "Normal equation matrix is singular; factors may be collinear.");
            }

            // Eliminate below
            for (var row = col + 1; row < n; row++)
            {
                var factor = aug[row, col] / aug[col, col];
                for (var j = col; j <= n; j++)
                {
                    aug[row, j] -= factor * aug[col, j];
                }
            }
        }

        // Back substitution
        var result = new double[n];
        for (var i = n - 1; i >= 0; i--)
        {
            var sum = aug[i, n];
            for (var j = i + 1; j < n; j++)
            {
                sum -= aug[i, j] * result[j];
            }

            result[i] = sum / aug[i, i];
        }

        return result;
    }
}
