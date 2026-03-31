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

namespace Boutquin.Trading.Tests.UnitTests.Application.Calendar;

using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for trading calendar integration with rebalancing date snapping.
/// </summary>
public sealed class RebalancingCalendarSnapTests
{
    private static readonly Asset s_aapl = new("AAPL");
    private static readonly Asset s_msft = new("MSFT");

    private static IReadOnlyDictionary<Asset, CurrencyCode> CreateAssets() =>
        new Dictionary<Asset, CurrencyCode>
        {
            [s_aapl] = CurrencyCode.USD,
            [s_msft] = CurrencyCode.USD,
        };

    private static SortedDictionary<CurrencyCode, decimal> CreateCash() =>
        new() { [CurrencyCode.USD] = 100_000m };

    private static IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> CreateMarketData(
        DateOnly start, int days)
    {
        var data = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
        for (var i = 0; i < days; i++)
        {
            var date = start.AddDays(i);
            data[date] = new SortedDictionary<Asset, MarketData>
            {
                [s_aapl] = new(date, 100m + i, 105m + i, 95m + i, 100m + i, 100m + i, 1_000_000L, 0m, 1m),
                [s_msft] = new(date, 200m + i, 205m + i, 195m + i, 200m + i, 200m + i, 1_000_000L, 0m, 1m),
            };
        }

        return data;
    }

    private static IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> CreateFxRates(
        DateOnly start, int days)
    {
        var data = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();
        for (var i = 0; i < days; i++)
        {
            data[start.AddDays(i)] = new SortedDictionary<CurrencyCode, decimal>
            {
                [CurrencyCode.USD] = 1m,
            };
        }

        return data;
    }

    // --- ConstructionModelStrategy tests ---

    [Fact]
    public void ConstructionModel_NoCalendar_UsesRawCalendarMath()
    {
        // Monthly rebalance: Jan 2 + 1 month = Feb 2
        // Without calendar, Feb 2 (even if weekend) triggers rebalance
        var strategy = new ConstructionModelStrategy(
            "test", CreateAssets(), CreateCash(),
            new ClosePriceOrderPriceCalculationStrategy(),
            new FixedWeightPositionSizer(new Dictionary<Asset, decimal>
            {
                [s_aapl] = 0.5m,
                [s_msft] = 0.5m,
            }, CurrencyCode.USD),
            new Boutquin.Trading.Application.PortfolioConstruction.EqualWeightConstruction(),
            RebalancingFrequency.Monthly);

        // First call at Jan 2 — always triggers (initial)
        var marketData = CreateMarketData(new DateOnly(2025, 1, 2), 70);
        var fxRates = CreateFxRates(new DateOnly(2025, 1, 2), 70);
        var firstSignals = strategy.GenerateSignals(
            new DateOnly(2025, 1, 2), CurrencyCode.USD, marketData, fxRates);

        firstSignals.Signals.Should().NotBeEmpty("first call always generates signals");
        firstSignals.Signals.Should().ContainKey(s_aapl);
        firstSignals.Signals.Should().ContainKey(s_msft);

        // Feb 1 should NOT trigger (< Feb 2)
        var noRebalance = strategy.GenerateSignals(
            new DateOnly(2025, 2, 1), CurrencyCode.USD, marketData, fxRates);
        noRebalance.Signals.Should().BeEmpty("Feb 1 is before Feb 2 rebalance date");

        // Feb 2 SHOULD trigger (>= Feb 2) — Sunday, but no calendar to snap
        var rebalance = strategy.GenerateSignals(
            new DateOnly(2025, 2, 2), CurrencyCode.USD, marketData, fxRates);
        rebalance.Signals.Should().NotBeEmpty("Feb 2 >= next rebalance date without calendar snap");
        rebalance.Signals.Should().ContainKey(s_aapl);
        rebalance.Signals.Should().ContainKey(s_msft);
        rebalance.Signals[s_aapl].Should().Be(SignalType.Rebalance);
        rebalance.Signals[s_msft].Should().Be(SignalType.Rebalance);
    }

    [Fact]
    public void ConstructionModel_WithCalendar_SnapsWeekendToMonday()
    {
        // Monthly rebalance: Jan 2 + 1 month = Feb 2 (Sunday in 2025)
        // Calendar should snap to Feb 3 (Monday)
        var mockCalendar = new Mock<ITradingCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(new DateOnly(2025, 2, 2))).Returns(false); // Sunday
        mockCalendar.Setup(c => c.NextTradingDay(new DateOnly(2025, 2, 2))).Returns(new DateOnly(2025, 2, 3)); // Monday
        mockCalendar.Setup(c => c.IsTradingDay(It.Is<DateOnly>(d => d != new DateOnly(2025, 2, 2)))).Returns(true);

        var strategy = new ConstructionModelStrategy(
            "test", CreateAssets(), CreateCash(),
            new ClosePriceOrderPriceCalculationStrategy(),
            new FixedWeightPositionSizer(new Dictionary<Asset, decimal>
            {
                [s_aapl] = 0.5m,
                [s_msft] = 0.5m,
            }, CurrencyCode.USD),
            new Boutquin.Trading.Application.PortfolioConstruction.EqualWeightConstruction(),
            RebalancingFrequency.Monthly,
            tradingCalendar: mockCalendar.Object);

        var marketData = CreateMarketData(new DateOnly(2025, 1, 2), 70);
        var fxRates = CreateFxRates(new DateOnly(2025, 1, 2), 70);

        // Initial trigger
        strategy.GenerateSignals(new DateOnly(2025, 1, 2), CurrencyCode.USD, marketData, fxRates);

        // Feb 2 (Sunday) should NOT trigger — snapped to Feb 3
        var feb2 = strategy.GenerateSignals(
            new DateOnly(2025, 2, 2), CurrencyCode.USD, marketData, fxRates);
        feb2.Signals.Should().BeEmpty("Feb 2 is before snapped date Feb 3");

        // Feb 3 (Monday) SHOULD trigger
        var feb3 = strategy.GenerateSignals(
            new DateOnly(2025, 2, 3), CurrencyCode.USD, marketData, fxRates);
        feb3.Signals.Should().NotBeEmpty("Feb 3 >= snapped rebalance date");
    }

    [Fact]
    public void ConstructionModel_WithCalendar_TradingDayUnchanged()
    {
        // If next rebalance date is already a trading day, no snap needed
        var mockCalendar = new Mock<ITradingCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(It.IsAny<DateOnly>())).Returns(true);

        var strategy = new ConstructionModelStrategy(
            "test", CreateAssets(), CreateCash(),
            new ClosePriceOrderPriceCalculationStrategy(),
            new FixedWeightPositionSizer(new Dictionary<Asset, decimal>
            {
                [s_aapl] = 0.5m,
                [s_msft] = 0.5m,
            }, CurrencyCode.USD),
            new Boutquin.Trading.Application.PortfolioConstruction.EqualWeightConstruction(),
            RebalancingFrequency.Monthly,
            tradingCalendar: mockCalendar.Object);

        var marketData = CreateMarketData(new DateOnly(2025, 1, 2), 70);
        var fxRates = CreateFxRates(new DateOnly(2025, 1, 2), 70);

        strategy.GenerateSignals(new DateOnly(2025, 1, 2), CurrencyCode.USD, marketData, fxRates);

        // Feb 2 is a trading day — should trigger normally (>= Feb 2)
        var feb2 = strategy.GenerateSignals(
            new DateOnly(2025, 2, 2), CurrencyCode.USD, marketData, fxRates);
        feb2.Signals.Should().NotBeEmpty("Feb 2 is a trading day, triggers normally");

        // NextTradingDay should NOT be called since IsTradingDay returns true
        mockCalendar.Verify(c => c.NextTradingDay(It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public void ConstructionModel_WithCalendar_Holiday_SnapsForward()
    {
        // Quarterly rebalance: Jan 2 + 3 months = Apr 2 (Wednesday in 2025)
        // Suppose Apr 2 is a holiday — snap to Apr 3
        var mockCalendar = new Mock<ITradingCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(new DateOnly(2025, 4, 2))).Returns(false);
        mockCalendar.Setup(c => c.NextTradingDay(new DateOnly(2025, 4, 2))).Returns(new DateOnly(2025, 4, 3));
        mockCalendar.Setup(c => c.IsTradingDay(It.Is<DateOnly>(d => d != new DateOnly(2025, 4, 2)))).Returns(true);

        var strategy = new ConstructionModelStrategy(
            "test", CreateAssets(), CreateCash(),
            new ClosePriceOrderPriceCalculationStrategy(),
            new FixedWeightPositionSizer(new Dictionary<Asset, decimal>
            {
                [s_aapl] = 0.5m,
                [s_msft] = 0.5m,
            }, CurrencyCode.USD),
            new Boutquin.Trading.Application.PortfolioConstruction.EqualWeightConstruction(),
            RebalancingFrequency.Quarterly,
            tradingCalendar: mockCalendar.Object);

        var marketData = CreateMarketData(new DateOnly(2025, 1, 2), 120);
        var fxRates = CreateFxRates(new DateOnly(2025, 1, 2), 120);

        strategy.GenerateSignals(new DateOnly(2025, 1, 2), CurrencyCode.USD, marketData, fxRates);

        // Apr 2 should NOT trigger — holiday snapped to Apr 3
        var apr2 = strategy.GenerateSignals(
            new DateOnly(2025, 4, 2), CurrencyCode.USD, marketData, fxRates);
        apr2.Signals.Should().BeEmpty("Apr 2 is a holiday; snapped to Apr 3");

        // Apr 3 SHOULD trigger
        var apr3 = strategy.GenerateSignals(
            new DateOnly(2025, 4, 3), CurrencyCode.USD, marketData, fxRates);
        apr3.Signals.Should().NotBeEmpty("Apr 3 >= snapped rebalance date");
    }

    // --- RebalancingBuyAndHoldStrategy tests ---

    [Fact]
    public void RebalancingBuyAndHold_NoCalendar_UsesRawCalendarMath()
    {
        var strategy = new RebalancingBuyAndHoldStrategy(
            "test", CreateAssets(), CreateCash(),
            new ClosePriceOrderPriceCalculationStrategy(),
            new FixedWeightPositionSizer(new Dictionary<Asset, decimal>
            {
                [s_aapl] = 0.5m,
                [s_msft] = 0.5m,
            }, CurrencyCode.USD),
            RebalancingFrequency.Monthly);

        var marketData = CreateMarketData(new DateOnly(2025, 1, 2), 70);
        var fxRates = CreateFxRates(new DateOnly(2025, 1, 2), 70);

        // First call triggers
        var first = strategy.GenerateSignals(
            new DateOnly(2025, 1, 2), CurrencyCode.USD, marketData, fxRates);
        first.Signals.Should().NotBeEmpty();

        // Feb 2 should trigger (>= Jan 2 + 1 month) even though Sunday
        var feb2 = strategy.GenerateSignals(
            new DateOnly(2025, 2, 2), CurrencyCode.USD, marketData, fxRates);
        feb2.Signals.Should().NotBeEmpty("raw calendar math: Feb 2 >= Jan 2 + 1 month");
    }

    [Fact]
    public void RebalancingBuyAndHold_WithCalendar_SnapsWeekendToMonday()
    {
        var mockCalendar = new Mock<ITradingCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(new DateOnly(2025, 2, 2))).Returns(false);
        mockCalendar.Setup(c => c.NextTradingDay(new DateOnly(2025, 2, 2))).Returns(new DateOnly(2025, 2, 3));
        mockCalendar.Setup(c => c.IsTradingDay(It.Is<DateOnly>(d => d != new DateOnly(2025, 2, 2)))).Returns(true);

        var strategy = new RebalancingBuyAndHoldStrategy(
            "test", CreateAssets(), CreateCash(),
            new ClosePriceOrderPriceCalculationStrategy(),
            new FixedWeightPositionSizer(new Dictionary<Asset, decimal>
            {
                [s_aapl] = 0.5m,
                [s_msft] = 0.5m,
            }, CurrencyCode.USD),
            RebalancingFrequency.Monthly,
            tradingCalendar: mockCalendar.Object);

        var marketData = CreateMarketData(new DateOnly(2025, 1, 2), 70);
        var fxRates = CreateFxRates(new DateOnly(2025, 1, 2), 70);

        strategy.GenerateSignals(new DateOnly(2025, 1, 2), CurrencyCode.USD, marketData, fxRates);

        // Feb 2 should NOT trigger — snapped to Feb 3
        var feb2 = strategy.GenerateSignals(
            new DateOnly(2025, 2, 2), CurrencyCode.USD, marketData, fxRates);
        feb2.Signals.Should().BeEmpty("Feb 2 snapped to Feb 3");

        // Feb 3 should trigger
        var feb3 = strategy.GenerateSignals(
            new DateOnly(2025, 2, 3), CurrencyCode.USD, marketData, fxRates);
        feb3.Signals.Should().NotBeEmpty("Feb 3 >= snapped date");
    }

    [Fact]
    public void RebalancingBuyAndHold_Constructor_BackwardCompatible()
    {
        // Old constructor (no calendar) still works
        var strategy = new RebalancingBuyAndHoldStrategy(
            "test", CreateAssets(), CreateCash(),
            new ClosePriceOrderPriceCalculationStrategy(),
            new FixedWeightPositionSizer(new Dictionary<Asset, decimal>
            {
                [s_aapl] = 0.5m,
                [s_msft] = 0.5m,
            }, CurrencyCode.USD),
            RebalancingFrequency.Monthly);

        strategy.Should().NotBeNull();
    }
}
