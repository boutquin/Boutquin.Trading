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

namespace Boutquin.Trading.Tests.UnitTests.Application;

using Boutquin.Trading.Application.Analytics;

/// <summary>
/// Tests for the trials-aware <see cref="WalkForwardOptimizer.RunWithTrials"/> overload, which
/// reports the trial count and the Deflated Sharpe Ratio of the selected parameter set for each
/// fold. The additive overload must leave the raw <see cref="WalkForwardOptimizer.Run"/> path
/// unchanged.
/// </summary>
public sealed class WalkForwardTrialsTests
{
    private const int InSampleDays = 200;
    private const int OutOfSampleDays = 100;
    private const int ParameterCount = 4;
    private const int TradingDaysPerYear = 252;

    // Deterministic evaluator shared by production call and the independent recomputation,
    // so the two see byte-identical trial Sharpes.
    private static decimal Eval(decimal[] returns, int paramIdx)
    {
        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Length - 1);
        var std = (decimal)Math.Sqrt((double)variance);
        var raw = std == 0m ? 0m : mean / std;
        return raw + paramIdx * 0.005m; // parameters produce distinct trial Sharpes
    }

    [Fact]
    public void RunWithTrials_ReportsTrialCountAndDeflatedSharpe_WiredThroughProductionPath()
    {
        var dates = BuildDatedReturns(450);
        var optimizer = new WalkForwardOptimizer(InSampleDays, OutOfSampleDays);

        var trialsResults = optimizer.RunWithTrials(dates, Eval, ParameterCount, TradingDaysPerYear);

        trialsResults.Should().NotBeEmpty();

        // Folds must match the raw path exactly (additive overload).
        var rawResults = optimizer.Run(dates, Eval, ParameterCount);
        trialsResults.Count.Should().Be(rawResults.Count);

        foreach (var r in trialsResults)
        {
            r.TrialsCount.Should().Be(ParameterCount);
            r.DeflatedSharpe.Should().BeGreaterThan(0m).And.BeLessThan(1m);
        }

        for (var i = 0; i < rawResults.Count; i++)
        {
            trialsResults[i].Fold.Should().Be(rawResults[i]);
        }
    }

    [Fact]
    public void RunWithTrials_FirstFoldDsr_EqualsIndependentlyRecomputedDsr_AntiInertnessPin()
    {
        var dates = BuildDatedReturns(450);
        var optimizer = new WalkForwardOptimizer(InSampleDays, OutOfSampleDays);

        var trialsResults = optimizer.RunWithTrials(dates, Eval, ParameterCount, TradingDaysPerYear);

        // Independently reconstruct the first fold's DSR through the public analytic.
        var values = dates.Values.ToArray();
        var inSample = values[0..InSampleDays];

        var trialSharpes = new decimal[ParameterCount];
        for (var p = 0; p < ParameterCount; p++)
        {
            trialSharpes[p] = Eval(inSample, p);
        }

        var bestSharpe = trialSharpes.Max();
        var mean = trialSharpes.Average();
        var variance = trialSharpes.Sum(s => (s - mean) * (s - mean)) / (ParameterCount - 1);

        var expected = DeflatedSharpeRatio.FromReturns(
            inSample, bestSharpe, ParameterCount, variance, TradingDaysPerYear);

        trialsResults[0].DeflatedSharpe.Should().Be(expected,
            "the overload must genuinely compute the DSR from the fold, not return a constant");
    }

    [Fact]
    public void RunWithTrials_SingleTrial_Throws()
    {
        var dates = BuildDatedReturns(450);
        var optimizer = new WalkForwardOptimizer(InSampleDays, OutOfSampleDays);

        var act = () => optimizer.RunWithTrials(dates, Eval, parameterCount: 1, TradingDaysPerYear);

        act.Should().Throw<ArgumentOutOfRangeException>(
            "the Deflated Sharpe Ratio requires at least two trials to form a trial-Sharpe variance");
    }

    private static SortedDictionary<DateOnly, decimal> BuildDatedReturns(int count)
    {
        var dict = new SortedDictionary<DateOnly, decimal>();
        var baseDate = new DateOnly(2020, 1, 1);
        var rng = new Random(1234);
        for (var i = 0; i < count; i++)
        {
            dict[baseDate.AddDays(i)] = (decimal)(rng.NextDouble() * 0.02 - 0.009);
        }

        return dict;
    }
}
