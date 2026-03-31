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

using Boutquin.Trading.Application.Analytics;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for Monte Carlo simulator and
/// walk-forward optimizer.
///
/// Phase 5E-5F of the verification roadmap.
/// Vectors: tests/Verification/vectors/monte_carlo.json, walk_forward.json
///
/// NOTE: Monte Carlo uses property-based verification because C# System.Random
/// and Python numpy.random produce different sequences from the same seed.
/// Walk-forward uses exact verification with own-formula Python.
/// </summary>
public sealed class MiscCrossLanguageTests : CrossLanguageVerificationBase
{
    // ═══════════════════════════════════════════════════════════════════════
    // 5E. Monte Carlo — Property-based verification
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MonteCarlo_StandardProperties()
    {
        using var doc = LoadVector("monte_carlo");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("standard");

        var dailyReturns = GetDecimalArray(testCase.GetProperty("daily_returns"));
        var simCount = testCase.GetProperty("simulation_count").GetInt32();
        var seed = testCase.GetProperty("seed").GetInt32();
        var tradingDays = testCase.GetProperty("trading_days_per_year").GetInt32();

        var simulator = new MonteCarloSimulator(simCount, seed, tradingDays);
        var result = simulator.Run(dailyReturns);

        // Property: simulation count matches
        Assert.Equal(simCount, result.SimulationCount);

        // Property: p5 <= median <= p95
        Assert.True(result.Percentile5Sharpe <= result.MedianSharpe,
            $"p5 ({result.Percentile5Sharpe}) should be <= median ({result.MedianSharpe})");
        Assert.True(result.MedianSharpe <= result.Percentile95Sharpe,
            $"median ({result.MedianSharpe}) should be <= p95 ({result.Percentile95Sharpe})");

        // Property: mean is between p5 and p95
        Assert.True(result.Percentile5Sharpe <= result.MeanSharpe,
            $"p5 ({result.Percentile5Sharpe}) should be <= mean ({result.MeanSharpe})");
        Assert.True(result.MeanSharpe <= result.Percentile95Sharpe,
            $"mean ({result.MeanSharpe}) should be <= p95 ({result.Percentile95Sharpe})");

        // Property: median close to empirical Sharpe (within 1.0)
        var empiricalSharpe = GetDecimal(testCase, "empirical_sharpe");
        var medianDiff = Math.Abs(result.MedianSharpe - empiricalSharpe);
        Assert.True(medianDiff < 1.0m,
            $"Median Sharpe ({result.MedianSharpe}) should be within 1.0 of empirical ({empiricalSharpe}), diff={medianDiff}");

        // Property: Sharpe ratios are sorted
        var sharpes = result.SharpeRatios;
        for (var i = 1; i < sharpes.Count; i++)
        {
            Assert.True(sharpes[i] >= sharpes[i - 1],
                $"SharpeRatios should be sorted: [{i - 1}]={sharpes[i - 1]}, [{i}]={sharpes[i]}");
        }
    }

    [Fact]
    public void MonteCarlo_ConstantReturns_AllSharpeZero()
    {
        using var doc = LoadVector("monte_carlo");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("constant_returns");

        var dailyReturns = GetDecimalArray(testCase.GetProperty("daily_returns"));
        var simCount = testCase.GetProperty("simulation_count").GetInt32();
        var seed = testCase.GetProperty("seed").GetInt32();
        var tradingDays = testCase.GetProperty("trading_days_per_year").GetInt32();

        var simulator = new MonteCarloSimulator(simCount, seed, tradingDays);
        var result = simulator.Run(dailyReturns);

        // C# decimal: constant returns => std = exactly 0 => Sharpe = 0
        // (Python float has residual noise, see generator note)
        Assert.Equal(0m, result.MedianSharpe);
        Assert.Equal(0m, result.MeanSharpe);
        Assert.Equal(0m, result.Percentile5Sharpe);
        Assert.Equal(0m, result.Percentile95Sharpe);
    }

    [Fact]
    public void MonteCarlo_SmallSample_Properties()
    {
        using var doc = LoadVector("monte_carlo");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("small_sample");

        var dailyReturns = GetDecimalArray(testCase.GetProperty("daily_returns"));
        var simCount = testCase.GetProperty("simulation_count").GetInt32();
        var seed = testCase.GetProperty("seed").GetInt32();
        var tradingDays = testCase.GetProperty("trading_days_per_year").GetInt32();

        var simulator = new MonteCarloSimulator(simCount, seed, tradingDays);
        var result = simulator.Run(dailyReturns);

        // Property checks still hold
        Assert.True(result.Percentile5Sharpe <= result.MedianSharpe);
        Assert.True(result.MedianSharpe <= result.Percentile95Sharpe);
        Assert.Equal(simCount, result.SimulationCount);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5F. Walk-Forward Optimizer
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void WalkForward_StandardFolds()
    {
        using var doc = LoadVector("walk_forward");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("standard");

        var datedReturnsArray = testCase.GetProperty("dated_returns").EnumerateArray().ToArray();
        var isDays = testCase.GetProperty("in_sample_days").GetInt32();
        var oosDays = testCase.GetProperty("out_of_sample_days").GetInt32();
        var paramCount = testCase.GetProperty("parameter_count").GetInt32();
        var expectedFolds = testCase.GetProperty("folds").EnumerateArray().ToArray();

        // Build dated returns
        var datedReturns = new SortedDictionary<DateOnly, decimal>();
        foreach (var entry in datedReturnsArray)
        {
            var dateStr = entry.GetProperty("date").GetString()!;
            var ret = (decimal)entry.GetProperty("return").GetDouble();
            datedReturns[DateOnly.Parse(dateStr)] = ret;
        }

        // Parameter evaluator: Sharpe of (returns + (p-1)*0.001), matching Python
        static decimal ParameterEvaluator(decimal[] returns, int p)
        {
            if (returns.Length < 2)
            {
                return 0m;
            }

            var adjustment = (p - 1) * 0.001m;
            var adjusted = new decimal[returns.Length];
            for (var i = 0; i < returns.Length; i++)
            {
                adjusted[i] = returns[i] + adjustment;
            }

            var mean = adjusted.Average();
            var sumSqDev = adjusted.Sum(r => (r - mean) * (r - mean));
            var stdDev = (decimal)Math.Sqrt((double)(sumSqDev / (adjusted.Length - 1)));

            return stdDev == 0m ? 0m : (mean / stdDev) * (decimal)Math.Sqrt(252);
        }

        var optimizer = new WalkForwardOptimizer(isDays, oosDays);
        var results = optimizer.Run(datedReturns, ParameterEvaluator, paramCount);

        // Verify fold count
        Assert.Equal(expectedFolds.Length, results.Count);

        for (var i = 0; i < results.Count; i++)
        {
            var actual = results[i];
            var expected = expectedFolds[i];

            // Verify fold index
            Assert.Equal(expected.GetProperty("fold_index").GetInt32(), actual.FoldIndex);

            // Verify dates
            Assert.Equal(
                DateOnly.Parse(expected.GetProperty("is_start").GetString()!),
                actual.InSampleStart);
            Assert.Equal(
                DateOnly.Parse(expected.GetProperty("is_end").GetString()!),
                actual.InSampleEnd);
            Assert.Equal(
                DateOnly.Parse(expected.GetProperty("oos_start").GetString()!),
                actual.OutOfSampleStart);
            Assert.Equal(
                DateOnly.Parse(expected.GetProperty("oos_end").GetString()!),
                actual.OutOfSampleEnd);

            // Verify selected parameter
            Assert.Equal(
                expected.GetProperty("selected_parameter").GetInt32(),
                actual.SelectedParameterIndex);

            // Verify Sharpe ratios (PrecisionNumeric due to float↔decimal in sqrt)
            var expectedIsSharpe = (decimal)expected.GetProperty("is_sharpe").GetDouble();
            var expectedOosSharpe = (decimal)expected.GetProperty("oos_sharpe").GetDouble();

            AssertWithinTolerance(actual.InSampleSharpe, expectedIsSharpe, PrecisionNumeric,
                $"Fold {i} IS Sharpe: ");
            AssertWithinTolerance(actual.OutOfSampleSharpe, expectedOosSharpe, PrecisionNumeric,
                $"Fold {i} OOS Sharpe: ");

            // Layer 2: IS end < OOS start (no look-ahead)
            Assert.True(actual.InSampleEnd < actual.OutOfSampleStart,
                $"Fold {i}: IS end ({actual.InSampleEnd}) must be before OOS start ({actual.OutOfSampleStart})");
        }
    }
}
