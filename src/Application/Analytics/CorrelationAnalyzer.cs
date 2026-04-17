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

using NumericsStats = Boutquin.Numerics.Statistics;

namespace Boutquin.Trading.Application.Analytics;

/// <summary>
/// Computes correlation matrices and diversification ratios for portfolio assets.
/// </summary>
public static class CorrelationAnalyzer
{
    private static readonly NumericsStats.SampleCovarianceEstimator s_covEstimator = new();

    /// <summary>
    /// Computes the full correlation matrix and diversification ratio for a set of assets.
    /// </summary>
    /// <param name="assetNames">Ordered asset names.</param>
    /// <param name="returns">N arrays of T returns each.</param>
    /// <param name="weights">N portfolio weights (must sum to 1).</param>
    /// <returns>A <see cref="CorrelationAnalysisResult"/> with the correlation matrix and diversification ratio.</returns>
    public static CorrelationAnalysisResult Analyze(
        IReadOnlyList<Symbol> assetNames,
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

        for (var i = 1; i < returns.Length; i++)
        {
            if (returns[i].Length != returns[0].Length)
            {
                throw new ArgumentException(
                    $"All return arrays must have the same length. returns[0].Length={returns[0].Length}, returns[{i}].Length={returns[i].Length}.",
                    nameof(returns));
            }
        }

        var n = assetNames.Count;

        var cov = s_covEstimator.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());

        // Std devs from covariance diagonal
        var stdDevs = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            stdDevs[i] = (decimal)Math.Sqrt((double)cov[i, i]);
        }

        // Correlation matrix: normalize covariance by pair-wise std devs
        var corr = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                corr[i, j] = stdDevs[i] == 0m || stdDevs[j] == 0m
                    ? (i == j ? 1m : 0m)
                    : cov[i, j] / (stdDevs[i] * stdDevs[j]);
                corr[j, i] = corr[i, j];
            }
        }

        // DR = Σ(w_i * σ_i) / σ_portfolio
        var weightedAvgVol = 0m;
        for (var i = 0; i < n; i++)
        {
            weightedAvgVol += weights[i] * stdDevs[i];
        }

        var portfolioVariance = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                portfolioVariance += weights[i] * weights[j] * cov[i, j];
            }
        }

        var portfolioVol = (decimal)Math.Sqrt((double)portfolioVariance);
        var diversificationRatio = portfolioVol > 0m ? weightedAvgVol / portfolioVol : 1m;

        return new CorrelationAnalysisResult(corr, assetNames, diversificationRatio);
    }

    /// <summary>
    /// Computes a rolling correlation time series between two return series.
    /// </summary>
    /// <param name="returnsA">First asset return series.</param>
    /// <param name="returnsB">Second asset return series.</param>
    /// <param name="windowSize">The rolling window size.</param>
    /// <returns>An array of rolling correlation values. Length = T - windowSize + 1.</returns>
    public static decimal[] RollingCorrelation(decimal[] returnsA, decimal[] returnsB, int windowSize)
        => NumericsStats.PearsonCorrelation.Rolling(returnsA, returnsB, windowSize);
}
