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

public sealed class WalkForwardTests
{
    [Fact]
    public void WalkForward_Run_ShouldProduceFolds()
    {
        // 100 days, IS=50, OOS=25 → 2 folds
        var dates = new SortedDictionary<DateOnly, decimal>();
        var baseDate = new DateOnly(2020, 1, 1);
        for (var i = 0; i < 100; i++)
        {
            dates[baseDate.AddDays(i)] = 0.001m * (i % 2 == 0 ? 1 : -1);
        }

        var optimizer = new WalkForwardOptimizer(50, 25);

        var results = optimizer.Run(dates, (returns, paramIdx) =>
        {
            // Simple evaluator: param 0 = average, param 1 = sum
            return paramIdx == 0 ? returns.Average() : returns.Sum();
        }, 2);

        results.Count.Should().Be(2);
        results[0].FoldIndex.Should().Be(0);
        results[1].FoldIndex.Should().Be(1);
    }

    [Fact]
    public void WalkForward_Run_OOSResultsDifferFromIS()
    {
        var dates = new SortedDictionary<DateOnly, decimal>();
        var baseDate = new DateOnly(2020, 1, 1);
        var rng = new Random(42);
        for (var i = 0; i < 200; i++)
        {
            dates[baseDate.AddDays(i)] = (decimal)(rng.NextDouble() * 0.02 - 0.01);
        }

        var optimizer = new WalkForwardOptimizer(100, 50);
        var results = optimizer.Run(dates, (returns, _) => returns.Average() / (returns.Length > 1
            ? (decimal)Math.Sqrt((double)(returns.Sum(r => (r - returns.Average()) * (r - returns.Average())) / (returns.Length - 1)))
            : 1m), 1);

        results.Should().NotBeEmpty();
        // IS and OOS Sharpe should generally differ (not identical)
        results[0].InSampleSharpe.Should().NotBe(results[0].OutOfSampleSharpe);
    }

    [Fact]
    public void WalkForward_Run_NoLookAheadBias()
    {
        var dates = new SortedDictionary<DateOnly, decimal>();
        var baseDate = new DateOnly(2020, 1, 1);
        for (var i = 0; i < 100; i++)
        {
            dates[baseDate.AddDays(i)] = 0.001m;
        }

        var optimizer = new WalkForwardOptimizer(50, 25);
        var results = optimizer.Run(dates, (_, _) => 1m, 1);

        // OOS start must be after IS end
        foreach (var result in results)
        {
            result.OutOfSampleStart.Should().BeAfter(result.InSampleEnd);
        }
    }

    [Fact]
    public void WalkForward_Run_InsufficientData_ShouldThrow()
    {
        var dates = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2020, 1, 1)] = 0.01m,
            [new DateOnly(2020, 1, 2)] = -0.01m,
        };

        var optimizer = new WalkForwardOptimizer(50, 25);
        var act = () => optimizer.Run(dates, (_, _) => 1m, 1);
        act.Should().Throw<InsufficientDataException>();
    }

    [Fact]
    public void WalkForward_Run_ParameterSelectedFromInSample()
    {
        var dates = new SortedDictionary<DateOnly, decimal>();
        var baseDate = new DateOnly(2020, 1, 1);
        for (var i = 0; i < 100; i++)
        {
            dates[baseDate.AddDays(i)] = 0.001m;
        }

        var optimizer = new WalkForwardOptimizer(50, 25);
        var results = optimizer.Run(dates, (_, paramIdx) =>
        {
            // Param 2 always returns highest Sharpe
            return paramIdx * 0.5m;
        }, 3);

        // Should select param 2 (highest)
        results[0].SelectedParameterIndex.Should().Be(2);
    }
}
