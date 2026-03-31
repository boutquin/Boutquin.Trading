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

using Boutquin.Trading.Domain.Analytics;

/// <summary>
/// Performs Monte Carlo bootstrap resampling of daily returns to produce
/// a distribution of Sharpe ratios for robustness testing.
/// </summary>
public sealed class MonteCarloSimulator
{
    private readonly int _simulationCount;
    private readonly int _seed;
    private readonly int _tradingDaysPerYear;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonteCarloSimulator"/> class.
    /// </summary>
    /// <param name="simulationCount">Number of bootstrap simulations to run.</param>
    /// <param name="seed">Random seed for reproducibility. Default -1 (non-deterministic).</param>
    /// <param name="tradingDaysPerYear">Trading days per year for Sharpe ratio annualization. Default 252.</param>
    public MonteCarloSimulator(int simulationCount = 1000, int seed = -1, int tradingDaysPerYear = 252)
    {
        Guard.AgainstNegativeOrZero(() => simulationCount);
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear);
        _simulationCount = simulationCount;
        _seed = seed;
        _tradingDaysPerYear = tradingDaysPerYear;
    }

    /// <summary>
    /// Runs Monte Carlo bootstrap simulation on the given daily returns.
    /// Each simulation resamples with replacement to create a synthetic return series
    /// of the same length, then computes the Sharpe ratio.
    /// </summary>
    /// <param name="dailyReturns">The original daily returns to resample from.</param>
    /// <returns>A <see cref="MonteCarloResult"/> with the distribution of Sharpe ratios.</returns>
    public MonteCarloResult Run(decimal[] dailyReturns)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        if (dailyReturns.Length < 2)
        {
            throw new InsufficientDataException(
                "Need at least 2 daily returns for Monte Carlo simulation.");
        }

        var rng = _seed >= 0 ? new Random(_seed) : new Random();
        var sharpes = new decimal[_simulationCount];

        for (var sim = 0; sim < _simulationCount; sim++)
        {
            var resampled = new decimal[dailyReturns.Length];
            for (var i = 0; i < resampled.Length; i++)
            {
                resampled[i] = dailyReturns[rng.Next(dailyReturns.Length)];
            }

            var mean = resampled.Average();
            var sumSqDev = resampled.Sum(r => (r - mean) * (r - mean));
            var stdDev = (decimal)Math.Sqrt((double)(sumSqDev / (resampled.Length - 1)));

            sharpes[sim] = stdDev == 0m ? 0m : (mean / stdDev) * (decimal)Math.Sqrt(_tradingDaysPerYear);
        }

        Array.Sort(sharpes);

        return new MonteCarloResult(
            SimulationCount: _simulationCount,
            SharpeRatios: Array.AsReadOnly(sharpes),
            MedianSharpe: Percentile(sharpes, 0.50m),
            Percentile5Sharpe: Percentile(sharpes, 0.05m),
            Percentile95Sharpe: Percentile(sharpes, 0.95m),
            MeanSharpe: sharpes.Average());
    }

    private static decimal Percentile(decimal[] sortedValues, decimal percentile)
    {
        var index = (double)percentile * (sortedValues.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = Math.Min(lower + 1, sortedValues.Length - 1);
        var fraction = (decimal)(index - lower);
        return sortedValues[lower] + fraction * (sortedValues[upper] - sortedValues[lower]);
    }
}
