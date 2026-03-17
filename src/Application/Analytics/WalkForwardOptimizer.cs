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
/// Performs walk-forward analysis by splitting a return series into consecutive
/// in-sample / out-of-sample windows, selecting the best parameter set in-sample,
/// and evaluating it out-of-sample.
/// </summary>
public sealed class WalkForwardOptimizer
{
    private readonly int _inSampleDays;
    private readonly int _outOfSampleDays;

    /// <summary>
    /// Initializes a new instance of the <see cref="WalkForwardOptimizer"/> class.
    /// </summary>
    /// <param name="inSampleDays">Number of trading days for each in-sample window.</param>
    /// <param name="outOfSampleDays">Number of trading days for each out-of-sample window.</param>
    public WalkForwardOptimizer(int inSampleDays, int outOfSampleDays)
    {
        Guard.AgainstNegativeOrZero(() => inSampleDays);
        Guard.AgainstNegativeOrZero(() => outOfSampleDays);

        _inSampleDays = inSampleDays;
        _outOfSampleDays = outOfSampleDays;
    }

    /// <summary>
    /// Runs walk-forward optimization over the given dated returns.
    /// </summary>
    /// <param name="datedReturns">Daily returns keyed by date, sorted chronologically.</param>
    /// <param name="parameterEvaluator">
    /// Function that takes (returns array, parameter index) and returns a Sharpe ratio.
    /// Called once per parameter per window.
    /// </param>
    /// <param name="parameterCount">The number of parameter sets to evaluate.</param>
    /// <returns>A list of walk-forward results, one per fold.</returns>
    public IReadOnlyList<WalkForwardResult> Run(
        SortedDictionary<DateOnly, decimal> datedReturns,
        Func<decimal[], int, decimal> parameterEvaluator,
        int parameterCount)
    {
        Guard.AgainstNull(() => datedReturns);
        Guard.AgainstNull(() => parameterEvaluator);
        Guard.AgainstNegativeOrZero(() => parameterCount);

        var dates = datedReturns.Keys.ToList();
        var returns = datedReturns.Values.ToArray();

        if (dates.Count < _inSampleDays + _outOfSampleDays)
        {
            throw new InsufficientDataException(
                $"Need at least {_inSampleDays + _outOfSampleDays} data points for walk-forward, got {dates.Count}.");
        }

        var results = new List<WalkForwardResult>();
        var foldIndex = 0;
        var start = 0;

        while (start + _inSampleDays + _outOfSampleDays <= dates.Count)
        {
            var isEnd = start + _inSampleDays;
            var oosEnd = Math.Min(isEnd + _outOfSampleDays, dates.Count);

            var inSampleReturns = returns[start..isEnd];

            // Select best parameter in-sample
            var bestParam = 0;
            var bestSharpe = decimal.MinValue;

            for (var p = 0; p < parameterCount; p++)
            {
                var sharpe = parameterEvaluator(inSampleReturns, p);
                if (sharpe > bestSharpe)
                {
                    bestSharpe = sharpe;
                    bestParam = p;
                }
            }

            // Evaluate out-of-sample
            var oosReturns = returns[isEnd..oosEnd];
            var oosSharpe = parameterEvaluator(oosReturns, bestParam);

            results.Add(new WalkForwardResult(
                FoldIndex: foldIndex,
                InSampleStart: dates[start],
                InSampleEnd: dates[isEnd - 1],
                OutOfSampleStart: dates[isEnd],
                OutOfSampleEnd: dates[oosEnd - 1],
                SelectedParameterIndex: bestParam,
                InSampleSharpe: bestSharpe,
                OutOfSampleSharpe: oosSharpe));

            start += _outOfSampleDays; // Roll forward by OOS window
            foldIndex++;
        }

        return results;
    }
}
