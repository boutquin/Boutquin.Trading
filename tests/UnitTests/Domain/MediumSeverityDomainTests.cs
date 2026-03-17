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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// TDD tests verifying medium-severity domain fixes.
/// </summary>
public sealed class MediumSeverityDomainTests
{
    // ── BUG-D05: Zero split ratio must throw ──────────────────────────
    [Fact]
    public void MarketData_AdjustForSplit_ZeroRatio_Throws()
    {
        // Arrange
        var md = new MarketData(
            Timestamp: new DateOnly(2024, 1, 15),
            Open: 100m, High: 110m, Low: 90m, Close: 105m,
            AdjustedClose: 105m, Volume: 1_000_000,
            DividendPerShare: 0m, SplitCoefficient: 1m);

        // Act
        var act = () => md.AdjustForSplit(0m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarketData_AdjustForSplit_NegativeRatio_Throws()
    {
        // Arrange
        var md = new MarketData(
            Timestamp: new DateOnly(2024, 1, 15),
            Open: 100m, High: 110m, Low: 90m, Close: 105m,
            AdjustedClose: 105m, Volume: 1_000_000,
            DividendPerShare: 0m, SplitCoefficient: 1m);

        // Act
        var act = () => md.AdjustForSplit(-2m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarketData_AdjustForSplit_ValidRatio_Succeeds()
    {
        // Arrange
        var md = new MarketData(
            Timestamp: new DateOnly(2024, 1, 15),
            Open: 200m, High: 220m, Low: 180m, Close: 210m,
            AdjustedClose: 210m, Volume: 500_000,
            DividendPerShare: 0m, SplitCoefficient: 1m);

        // Act
        var result = md.AdjustForSplit(2m);

        // Assert
        result.Open.Should().Be(100m);
        result.Close.Should().Be(105m);
        result.Volume.Should().Be(1_000_000);
    }

    // ── BUG-D06: Peak equity initialized to first curve value ─────────
    [Fact]
    public void EquityCurveExtensions_PeakEquity_InitializedToFirstValue()
    {
        // Arrange — curve starts at 100, dips to 90, recovers to 110.
        // With peakEquity=0 (old bug): drawdown at day 1 would be 100/0 → crash or 0.
        // With peakEquity=first (fix): peak starts at 100, day 2 drawdown = 90/100 - 1 = -0.10.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2024, 1, 1), 100m },
            { new DateOnly(2024, 1, 2), 90m },
            { new DateOnly(2024, 1, 3), 110m },
        };

        // Act
        var (drawdowns, maxDrawdown, _) = equityCurve.CalculateDrawdownsAndMaxDrawdownInfo();

        // Assert — max drawdown is -10% (90/100 - 1), not some inflated value from peak=0.
        maxDrawdown.Should().BeApproximately(-0.10m, 1e-12m);
        drawdowns[new DateOnly(2024, 1, 1)].Should().Be(0m); // First day: at peak
        drawdowns[new DateOnly(2024, 1, 2)].Should().BeApproximately(-0.10m, 1e-12m);
        drawdowns[new DateOnly(2024, 1, 3)].Should().Be(0m); // New peak at 110
    }

    // ── TYP-D03: Asset with null/whitespace ticker throws ─────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Asset_NullOrWhiteSpaceTicker_ThrowsArgumentException(string? ticker)
    {
        // Act
        var act = () => new Asset(ticker!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Asset_ValidTicker_Succeeds()
    {
        // Act
        var asset = new Asset("AAPL");

        // Assert
        asset.Ticker.Should().Be("AAPL");
    }

    // ── API-D01: CAGR accepts int tradingDaysPerYear ──────────────────
    [Fact]
    public void CAGR_IntTradingDays_ConsistentWithOtherMethods()
    {
        // Arrange — simple daily returns array
        var dailyReturns = new[] { 0.01m, 0.02m, -0.005m, 0.015m, 0.01m };

        // Act — should compile and accept int parameter (was double before fix)
        int tradingDays = 252;
        var cagr = dailyReturns.CompoundAnnualGrowthRate(tradingDays);

        // Assert — value is positive and finite
        cagr.Should().BeGreaterThan(0m);
    }
}
