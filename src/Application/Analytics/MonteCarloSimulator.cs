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

using Boutquin.Numerics.MonteCarlo;
using Boutquin.Trading.Domain.Analytics;

namespace Boutquin.Trading.Application.Analytics;

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

        var sqrtDays = (decimal)Math.Sqrt(_tradingDaysPerYear);

        decimal AnnualizedSharpe(decimal[] r)
        {
            var mean = r.Average();
            var sumSq = r.Sum(x => (x - mean) * (x - mean));
            var std = (decimal)Math.Sqrt((double)(sumSq / (r.Length - 1)));
            return std == 0m ? 0m : (mean / std) * sqrtDays;
        }

        var result = BootstrapMonteCarloEngine.FromSeed(
                _simulationCount,
                _seed >= 0 ? _seed : null)
            .Run(dailyReturns, AnnualizedSharpe);

        return new MonteCarloResult(
            SimulationCount: _simulationCount,
            SharpeRatios: result.Statistics,
            MedianSharpe: result.Median,
            Percentile5Sharpe: result.Percentile5,
            Percentile95Sharpe: result.Percentile95,
            MeanSharpe: result.Mean);
    }
}
