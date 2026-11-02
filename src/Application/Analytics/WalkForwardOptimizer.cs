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

    /// <summary>
    /// Runs walk-forward optimization and additionally reports, per fold, the number of trials
    /// evaluated and the <b>Deflated Sharpe Ratio</b> of the selected parameter set.
    /// </summary>
    /// <remarks>
    /// This is an additive overload of <see cref="Run"/>: the fold selection and out-of-sample
    /// evaluation are identical (each returned <see cref="WalkForwardTrialsResult.Fold"/> equals
    /// the corresponding <see cref="Run"/> result). For each fold it further computes the Deflated
    /// Sharpe Ratio (Bailey &amp; López de Prado 2014) of the in-sample-selected parameter, treating
    /// the <paramref name="parameterCount"/> in-sample trials as the trial count <c>N</c> and their
    /// sample variance as <c>V[{ŜR_n}]</c> — the Harvey, Liu &amp; Zhu (2016) multiple-testing
    /// discipline the raw path lacks (it picks max in-sample Sharpe with no trial-count penalty).
    /// Higher moments come from the in-sample return series via
    /// <see cref="DeflatedSharpeRatio.FromReturns"/>, which applies the excess→full kurtosis conversion.
    /// </remarks>
    /// <param name="datedReturns">Daily returns keyed by date, sorted chronologically.</param>
    /// <param name="parameterEvaluator">
    /// Function that takes (returns array, parameter index) and returns a Sharpe ratio, at the
    /// <paramref name="tradingDaysPerYear"/> annualization.
    /// </param>
    /// <param name="parameterCount">The number of parameter sets to evaluate (must be ≥ 2 for the DSR).</param>
    /// <param name="tradingDaysPerYear">
    /// The annualization of the evaluator's Sharpe ratio (must be ≥ 1; default 1 = per-period).
    /// </param>
    /// <returns>A list of trials-aware walk-forward results, one per fold.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="parameterCount"/> &lt; 2 (a trial-Sharpe variance requires at least
    /// two trials) or <paramref name="tradingDaysPerYear"/> &lt; 1.
    /// </exception>
    public IReadOnlyList<WalkForwardTrialsResult> RunWithTrials(
        SortedDictionary<DateOnly, decimal> datedReturns,
        Func<decimal[], int, decimal> parameterEvaluator,
        int parameterCount,
        int tradingDaysPerYear = 1)
    {
        Guard.AgainstNull(() => datedReturns);
        Guard.AgainstNull(() => parameterEvaluator);

        if (parameterCount < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameterCount), parameterCount,
                "At least two trials are required to compute the Deflated Sharpe Ratio of the selected parameter.");
        }

        if (tradingDaysPerYear < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tradingDaysPerYear), tradingDaysPerYear, "Trading days per year must be at least 1.");
        }

        var folds = Run(datedReturns, parameterEvaluator, parameterCount);
        var returns = datedReturns.Values.ToArray();

        var results = new List<WalkForwardTrialsResult>(folds.Count);
        var start = 0;

        foreach (var fold in folds)
        {
            var inSampleReturns = returns[start..(start + _inSampleDays)];

            // Re-run the in-sample trials to obtain the trial-Sharpe distribution for the variance.
            var trialSharpes = new decimal[parameterCount];
            for (var p = 0; p < parameterCount; p++)
            {
                trialSharpes[p] = parameterEvaluator(inSampleReturns, p);
            }

            var mean = trialSharpes.Average();
            var variance = trialSharpes.Sum(s => (s - mean) * (s - mean)) / (parameterCount - 1);

            var deflatedSharpe = DeflatedSharpeRatio.FromReturns(
                inSampleReturns, fold.InSampleSharpe, parameterCount, variance, tradingDaysPerYear);

            results.Add(new WalkForwardTrialsResult(fold, parameterCount, deflatedSharpe));

            start += _outOfSampleDays; // Same roll-forward as Run.
        }

        return results;
    }
}

/// <summary>
/// A walk-forward fold augmented with the trials-aware overfitting diagnostics: the number of
/// parameter trials evaluated and the Deflated Sharpe Ratio of the selected parameter set.
/// </summary>
/// <param name="Fold">The underlying walk-forward fold (identical to the raw <see cref="WalkForwardOptimizer.Run"/> result).</param>
/// <param name="TrialsCount">The number of parameter sets evaluated in-sample for this fold.</param>
/// <param name="DeflatedSharpe">
/// The Deflated Sharpe Ratio (Bailey &amp; López de Prado 2014) of the in-sample-selected
/// parameter, in <c>(0, 1)</c> — a probability corrected for the trial count.
/// </param>
public sealed record WalkForwardTrialsResult(
    WalkForwardResult Fold,
    int TrialsCount,
    decimal DeflatedSharpe);
