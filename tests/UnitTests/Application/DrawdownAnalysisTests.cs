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
using FluentAssertions;

/// <summary>
/// Tests for detailed drawdown analysis — drawdown table with start, trough, recovery, depth, duration.
/// </summary>
public sealed class DrawdownAnalysisTests
{
    private const decimal Precision = 1e-10m;

    // --- RP3-04 Test: Known equity curve produces correct drawdown periods ---

    [Fact]
    public void Analyze_KnownEquityCurve_ShouldProduceCorrectDrawdownPeriods()
    {
        // Equity curve: 100 → 110 → 105 → 95 → 100 → 115 → 108 → 120
        // Drawdown 1: peak at day2 (110), trough at day4 (95), recovery at day6 (115)
        //   depth = (95-110)/110 = -0.136363...
        // Drawdown 2: peak at day6 (115), trough at day7 (108), recovery at day8 (120)
        //   depth = (108-115)/115 = -0.060869...
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2023, 1, 2), 100m },
            { new DateOnly(2023, 1, 3), 110m },
            { new DateOnly(2023, 1, 4), 105m },
            { new DateOnly(2023, 1, 5), 95m },
            { new DateOnly(2023, 1, 6), 100m },
            { new DateOnly(2023, 1, 9), 115m },
            { new DateOnly(2023, 1, 10), 108m },
            { new DateOnly(2023, 1, 11), 120m }
        };

        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        periods.Should().HaveCount(2, "Two separate drawdown periods should be identified");

        // First drawdown: peak 110 (Jan 3), trough 95 (Jan 5), recovery 115 (Jan 9)
        var first = periods[0];
        first.StartDate.Should().Be(new DateOnly(2023, 1, 3));
        first.TroughDate.Should().Be(new DateOnly(2023, 1, 5));
        first.RecoveryDate.Should().Be(new DateOnly(2023, 1, 9));
        first.Depth.Should().BeApproximately((95m - 110m) / 110m, Precision); // (95-110)/110 = -15/110

        // Second drawdown: peak 115 (Jan 9), trough 108 (Jan 10), recovery 120 (Jan 11)
        var second = periods[1];
        second.StartDate.Should().Be(new DateOnly(2023, 1, 9));
        second.TroughDate.Should().Be(new DateOnly(2023, 1, 10));
        second.RecoveryDate.Should().Be(new DateOnly(2023, 1, 11));
    }

    // --- RP3-04 Test: Ongoing drawdown has no recovery date ---

    [Fact]
    public void Analyze_OngoingDrawdown_ShouldHaveNullRecoveryDate()
    {
        // Equity goes up then down without recovery
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2023, 1, 2), 100m },
            { new DateOnly(2023, 1, 3), 110m },
            { new DateOnly(2023, 1, 4), 105m },
            { new DateOnly(2023, 1, 5), 95m },
            { new DateOnly(2023, 1, 6), 98m }
        };

        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        periods.Should().HaveCount(1);
        periods[0].RecoveryDate.Should().BeNull("Drawdown hasn't recovered");
        periods[0].RecoveryDays.Should().BeNull();
    }

    // --- RP3-04 Test: Monotonically increasing equity → no drawdowns ---

    [Fact]
    public void Analyze_MonotonicallyIncreasingEquity_ShouldReturnNoDrawdowns()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2023, 1, 2), 100m },
            { new DateOnly(2023, 1, 3), 105m },
            { new DateOnly(2023, 1, 4), 110m },
            { new DateOnly(2023, 1, 5), 115m },
            { new DateOnly(2023, 1, 6), 120m }
        };

        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        periods.Should().BeEmpty("No drawdowns in a monotonically increasing equity curve");
    }

    // --- RP3-04 Test: Single drawdown spanning entire series ---

    [Fact]
    public void Analyze_SingleLargeDrawdown_ShouldReturnOnePeriod()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2023, 1, 2), 100m },
            { new DateOnly(2023, 1, 3), 90m },
            { new DateOnly(2023, 1, 4), 80m },
            { new DateOnly(2023, 1, 5), 85m },
            { new DateOnly(2023, 1, 6), 75m }
        };

        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        periods.Should().HaveCount(1);
        periods[0].TroughDate.Should().Be(new DateOnly(2023, 1, 6));
        periods[0].Depth.Should().BeApproximately(-0.25m, Precision); // (75-100)/100
        periods[0].RecoveryDate.Should().BeNull();
    }

    // --- RP3-04 Test: Empty equity curve throws ---

    [Fact]
    public void Analyze_EmptyEquityCurve_ShouldThrow()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>();

        var act = () => DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        act.Should().Throw<ArgumentException>();
    }
}
