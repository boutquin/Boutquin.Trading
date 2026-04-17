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
/// using QR decomposition via <see cref="OrdinaryLeastSquares{T}"/>.
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

        // Build X matrix (T × K): rows = observations, columns = factor returns
        // OrdinaryLeastSquares.Fit prepends the intercept column when includeIntercept=true
        var x = new decimal[t, k];
        for (var i = 0; i < t; i++)
        {
            for (var j = 0; j < k; j++)
            {
                x[i, j] = factorReturns[j][i];
            }
        }

        OlsResult<decimal> result;
        try
        {
            result = OrdinaryLeastSquares<decimal>.Fit(x, portfolioReturns, includeIntercept: true);
        }
        catch (InvalidOperationException)
        {
            throw new CalculationException("Normal equation matrix is singular; factors may be collinear.");
        }

        // Coefficients[0] = alpha (intercept), Coefficients[1..k] = factor betas
        var factorLoadings = new Dictionary<string, decimal>();
        for (var j = 0; j < k; j++)
        {
            factorLoadings[factorNames[j]] = result.Coefficients[j + 1];
        }

        return new FactorRegressionResult(
            Alpha: result.Coefficients[0],
            FactorLoadings: factorLoadings,
            RSquared: result.RSquared,
            ResidualStandardError: result.ResidualStandardDeviation);
    }
}
