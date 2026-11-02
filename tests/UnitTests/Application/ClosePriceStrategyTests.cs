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

/// <summary>
/// Tests for <see cref="ClosePriceOrderPriceCalculationStrategy"/> (R2I-14).
/// </summary>
public sealed class ClosePriceStrategyTests
{
    private static readonly Symbol s_testAsset = new("AAPL");
    private static readonly DateOnly s_testDate = new(2024, 1, 15);

    private static IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> CreateHistoricalData(
        Symbol asset, DateOnly date, decimal close)
    {
        var md = new Bar(date, 100m, 105m, 95m, close, close, 1000000L);
        return new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            [date] = new SortedDictionary<Symbol, Bar> { [asset] = md }
        };
    }

    [Fact]
    public void CalculateOrderPrices_ValidData_ReturnsMarketOrderWithClosePrice()
    {
        var strategy = new ClosePriceOrderPriceCalculationStrategy();
        var historicalData = CreateHistoricalData(s_testAsset, s_testDate, 185.92m);

        var (orderType, primaryPrice, secondaryPrice) =
            strategy.CalculateOrderPrices(s_testDate, s_testAsset, TradeAction.Buy, historicalData);

        orderType.Should().Be(OrderType.Market);
        primaryPrice.Should().Be(185.92m);
        secondaryPrice.Should().BeNull();
    }

    [Fact]
    public void CalculateOrderPrices_SellAction_ReturnsSameClosePrice()
    {
        var strategy = new ClosePriceOrderPriceCalculationStrategy();
        var historicalData = CreateHistoricalData(s_testAsset, s_testDate, 185.92m);

        var (_, primaryPrice, _) =
            strategy.CalculateOrderPrices(s_testDate, s_testAsset, TradeAction.Sell, historicalData);

        primaryPrice.Should().Be(185.92m);
    }

    [Fact]
    public void CalculateOrderPrices_MultipleSymbols_ReturnsCorrectAsset()
    {
        var strategy = new ClosePriceOrderPriceCalculationStrategy();
        var msft = new Symbol("MSFT");
        var historicalData = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            [s_testDate] = new SortedDictionary<Symbol, Bar>
            {
                [s_testAsset] = new Bar(s_testDate, 100m, 105m, 95m, 185.92m, 185.92m, 1000000L),
                [msft] = new Bar(s_testDate, 400m, 410m, 395m, 405.50m, 405.50m, 500000L),
            }
        };

        var (_, priceAapl, _) = strategy.CalculateOrderPrices(s_testDate, s_testAsset, TradeAction.Buy, historicalData);
        var (_, priceMsft, _) = strategy.CalculateOrderPrices(s_testDate, msft, TradeAction.Buy, historicalData);

        priceAapl.Should().Be(185.92m);
        priceMsft.Should().Be(405.50m);
    }

    [Fact]
    public void CalculateOrderPrices_MissingDateWithinLookbackWindow_FallsBackToMostRecentBar()
    {
        // Updated semantics (2026-04-21): when the requested timestamp has no bar for the
        // asset but a prior bar exists within the 14-day cross-calendar lookback window,
        // the strategy returns the most recent prior bar's AdjustedClose rather than
        // throwing. This closes the class of "provider data gap mid-backtest" errors that
        // were aborting CA blueprints on random TSX trading days where TwelveData had a
        // single-day hole in the price series. Exception path still fires when the gap
        // exceeds the lookback — covered by the companion test below.
        var strategy = new ClosePriceOrderPriceCalculationStrategy();
        var historicalData = CreateHistoricalData(s_testAsset, s_testDate, 185.92m);

        // s_testDate = 2024-01-15; requesting 2024-01-20 (5 days later) falls within the
        // 14-day window, so the 2024-01-15 bar's AdjustedClose is returned.
        var (_, primaryPrice, _) = strategy.CalculateOrderPrices(
            new DateOnly(2024, 1, 20), s_testAsset, TradeAction.Buy, historicalData);

        primaryPrice.Should().Be(185.92m);
    }

    [Fact]
    public void CalculateOrderPrices_MissingDateBeyondLookbackWindow_ThrowsArgumentException()
    {
        var strategy = new ClosePriceOrderPriceCalculationStrategy();
        var historicalData = CreateHistoricalData(s_testAsset, s_testDate, 185.92m);

        // s_testDate = 2024-01-15; requesting 2024-02-15 (31 days later) exceeds the
        // 14-day lookback, so the fallback exhausts and the strategy throws as before.
        var act = () => strategy.CalculateOrderPrices(
            new DateOnly(2024, 2, 15), s_testAsset, TradeAction.Buy, historicalData);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*market data*asset*");
    }

    [Fact]
    public void CalculateOrderPrices_MissingSymbol_ThrowsArgumentException()
    {
        var strategy = new ClosePriceOrderPriceCalculationStrategy();
        var historicalData = CreateHistoricalData(s_testAsset, s_testDate, 185.92m);

        var act = () => strategy.CalculateOrderPrices(
            s_testDate, new Symbol("MISSING"), TradeAction.Buy, historicalData);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*market data*asset*");
    }
}
