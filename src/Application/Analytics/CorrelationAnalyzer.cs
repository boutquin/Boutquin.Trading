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
using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Application.Analytics;

/// <summary>
/// Computes correlation matrices and diversification ratios for portfolio assets.
/// </summary>
public static class CorrelationAnalyzer
{
    /// <summary>
    /// Computes the full correlation matrix and diversification ratio for a set of assets.
    /// </summary>
    /// <param name="assetNames">Ordered asset names.</param>
    /// <param name="returns">N arrays of T returns each.</param>
    /// <param name="weights">N portfolio weights (must sum to 1).</param>
    /// <returns>A <see cref="CorrelationAnalysisResult"/> with the correlation matrix and diversification ratio.</returns>
    public static CorrelationAnalysisResult Analyze(
        IReadOnlyList<Asset> assetNames,
        decimal[][] returns,
        decimal[] weights)
    {
        Guard.AgainstNull(() => assetNames);
        Guard.AgainstNull(() => returns);
        Guard.AgainstNull(() => weights);

        if (returns.Length != assetNames.Count)
        {
            throw new ArgumentException(
                $"Returns array length ({returns.Length}) must match asset count ({assetNames.Count}).",
                nameof(returns));
        }

        if (weights.Length != assetNames.Count)
        {
            throw new ArgumentException(
                $"Weights array length ({weights.Length}) must match asset count ({assetNames.Count}).",
                nameof(weights));
        }

        if (assetNames.Count > 0 && returns[0].Length < 2)
        {
            throw new ArgumentException(
                "Need at least 2 observations per asset for covariance computation (N-1 divisor).",
                nameof(returns));
        }

        var n = assetNames.Count;
        var t = returns[0].Length;

        // Compute means
        var means = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            means[i] = returns[i].Average();
        }

        // Compute covariance matrix (sample, N-1 divisor)
        var cov = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                var sum = 0m;
                for (var k = 0; k < t; k++)
                {
                    sum += (returns[i][k] - means[i]) * (returns[j][k] - means[j]);
                }

                cov[i, j] = sum / (t - 1);
                cov[j, i] = cov[i, j];
            }
        }

        // Compute standard deviations
        var stdDevs = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            stdDevs[i] = (decimal)Math.Sqrt((double)cov[i, i]);
        }

        // Compute correlation matrix
        var corr = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                if (stdDevs[i] == 0m || stdDevs[j] == 0m)
                {
                    corr[i, j] = i == j ? 1.0m : 0m;
                }
                else
                {
                    corr[i, j] = cov[i, j] / (stdDevs[i] * stdDevs[j]);
                }

                corr[j, i] = corr[i, j];
            }
        }

        // Compute diversification ratio:
        // DR = Σ(w_i * σ_i) / σ_portfolio
        var weightedAvgVol = 0m;
        for (var i = 0; i < n; i++)
        {
            weightedAvgVol += weights[i] * stdDevs[i];
        }

        // Portfolio variance = w' * Cov * w
        var portfolioVariance = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                portfolioVariance += weights[i] * weights[j] * cov[i, j];
            }
        }

        var portfolioVol = (decimal)Math.Sqrt((double)portfolioVariance);
        var diversificationRatio = portfolioVol > 0m ? weightedAvgVol / portfolioVol : 1.0m;

        return new CorrelationAnalysisResult(corr, assetNames, diversificationRatio);
    }

    /// <summary>
    /// Computes a rolling correlation time series between two return series.
    /// Note: This implementation recomputes each window from scratch. An incremental (online)
    /// algorithm is a future optimization for very large series.
    /// </summary>
    /// <param name="returnsA">First asset return series.</param>
    /// <param name="returnsB">Second asset return series.</param>
    /// <param name="windowSize">The rolling window size.</param>
    /// <returns>An array of rolling correlation values. Length = T - windowSize + 1.</returns>
    public static decimal[] RollingCorrelation(decimal[] returnsA, decimal[] returnsB, int windowSize)
    {
        if (returnsA.Length != returnsB.Length)
        {
            throw new ArgumentException("Return series must have the same length.", nameof(returnsB));
        }

        if (windowSize < 2 || windowSize > returnsA.Length)
        {
            throw new ArgumentException(
                $"Window size must be between 2 and {returnsA.Length}, but got {windowSize}.",
                nameof(windowSize));
        }

        var resultCount = returnsA.Length - windowSize + 1;
        var result = new decimal[resultCount];

        for (var start = 0; start < resultCount; start++)
        {
            var meanA = 0m;
            var meanB = 0m;

            for (var i = start; i < start + windowSize; i++)
            {
                meanA += returnsA[i];
                meanB += returnsB[i];
            }

            meanA /= windowSize;
            meanB /= windowSize;

            var covAB = 0m;
            var varA = 0m;
            var varB = 0m;

            for (var i = start; i < start + windowSize; i++)
            {
                var dA = returnsA[i] - meanA;
                var dB = returnsB[i] - meanB;
                covAB += dA * dB;
                varA += dA * dA;
                varB += dB * dB;
            }

            var denominator = (decimal)Math.Sqrt((double)(varA * varB));
            result[start] = denominator > 0m ? covAB / denominator : 0m;
        }

        return result;
    }
}
