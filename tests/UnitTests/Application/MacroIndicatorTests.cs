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

using Boutquin.Trading.Application.Indicators;
using Boutquin.Trading.Domain.Exceptions;

public sealed class MacroIndicatorTests
{
    private const decimal Precision = 1e-6m;

    // ============================================================
    // SpreadIndicator Tests (RP4-02)
    // ============================================================

    [Fact]
    public void SpreadIndicator_Compute_ShouldReturnDifference()
    {
        // 10Y yields - 2Y yields
        var indicator = new SpreadIndicator();
        var series10Y = new[] { 3.5m, 3.6m, 3.7m };
        var series2Y = new[] { 4.0m, 4.1m, 3.5m };
        // Latest: 3.7 - 3.5 = 0.2
        indicator.Compute(series10Y, series2Y).Should().Be(0.2m);
    }

    [Fact]
    public void SpreadIndicator_Compute_NegativeSpread_ShouldWork()
    {
        var indicator = new SpreadIndicator();
        var series1 = new[] { 1.0m };
        var series2 = new[] { 2.0m };
        indicator.Compute(series1, series2).Should().Be(-1.0m);
    }

    [Fact]
    public void SpreadIndicator_Compute_NullSeries_ShouldThrow()
    {
        var indicator = new SpreadIndicator();
        var act = () => indicator.Compute(null!, new[] { 1m });
        act.Should().Throw<EmptyOrNullArrayException>();
    }

    // ============================================================
    // RateOfChangeIndicator Tests (RP4-02)
    // ============================================================

    [Fact]
    public void RateOfChange_Compute_ShouldReturnCorrectValue()
    {
        var indicator = new RateOfChangeIndicator(2);
        // Spread at end: 10 - 5 = 5; Spread 2 back: 8 - 6 = 2
        // ROC = (5 - 2) / |2| = 1.5
        var series1 = new[] { 8m, 9m, 10m };
        var series2 = new[] { 6m, 5.5m, 5m };
        indicator.Compute(series1, series2).Should().BeApproximately(1.5m, Precision);
    }

    [Fact]
    public void RateOfChange_Compute_SpreadWidensDuringStress_ShouldBePositive()
    {
        var indicator = new RateOfChangeIndicator(1);
        // Credit spread: HYG yield - Treasury yield
        // Prior: 5.0 - 3.0 = 2.0
        // Current: 6.0 - 3.0 = 3.0
        // ROC = (3.0 - 2.0) / 2.0 = 0.5
        var hyg = new[] { 5.0m, 6.0m };
        var treasury = new[] { 3.0m, 3.0m };
        indicator.Compute(hyg, treasury).Should().BeApproximately(0.5m, Precision);
    }

    [Fact]
    public void RateOfChange_Compute_ZeroPriorSpread_ShouldThrow()
    {
        var indicator = new RateOfChangeIndicator(1);
        var series1 = new[] { 5m, 10m };
        var series2 = new[] { 5m, 3m };
        // Prior spread = 5-5 = 0
        var act = () => indicator.Compute(series1, series2);
        act.Should().Throw<CalculationException>();
    }

    [Fact]
    public void RateOfChange_Compute_UnequalLength_ShouldThrow()
    {
        var indicator = new RateOfChangeIndicator(1);
        var act = () => indicator.Compute(new[] { 1m, 2m }, new[] { 1m });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RateOfChange_Compute_InsufficientData_ShouldThrow()
    {
        var indicator = new RateOfChangeIndicator(5);
        var act = () => indicator.Compute(new[] { 1m, 2m }, new[] { 1m, 2m });
        act.Should().Throw<InsufficientDataException>();
    }
}
