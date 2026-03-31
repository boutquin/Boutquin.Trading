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
    private static readonly Asset s_testAsset = new("AAPL");
    private static readonly DateOnly s_testDate = new(2024, 1, 15);

    private static IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> CreateHistoricalData(
        Asset asset, DateOnly date, decimal close)
    {
        var md = new MarketData(date, 100m, 105m, 95m, close, close, 1000000L, 0m, 1m);
        return new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [date] = new SortedDictionary<Asset, MarketData> { [asset] = md }
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
        var msft = new Asset("MSFT");
        var historicalData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [s_testDate] = new SortedDictionary<Asset, MarketData>
            {
                [s_testAsset] = new MarketData(s_testDate, 100m, 105m, 95m, 185.92m, 185.92m, 1000000L, 0m, 1m),
                [msft] = new MarketData(s_testDate, 400m, 410m, 395m, 405.50m, 405.50m, 500000L, 0m, 1m),
            }
        };

        var (_, priceAapl, _) = strategy.CalculateOrderPrices(s_testDate, s_testAsset, TradeAction.Buy, historicalData);
        var (_, priceMsft, _) = strategy.CalculateOrderPrices(s_testDate, msft, TradeAction.Buy, historicalData);

        priceAapl.Should().Be(185.92m);
        priceMsft.Should().Be(405.50m);
    }

    [Fact]
    public void CalculateOrderPrices_MissingDate_ThrowsArgumentException()
    {
        var strategy = new ClosePriceOrderPriceCalculationStrategy();
        var historicalData = CreateHistoricalData(s_testAsset, s_testDate, 185.92m);

        var act = () => strategy.CalculateOrderPrices(
            new DateOnly(2024, 1, 20), s_testAsset, TradeAction.Buy, historicalData);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*market data*date*");
    }

    [Fact]
    public void CalculateOrderPrices_MissingSymbol_ThrowsArgumentException()
    {
        var strategy = new ClosePriceOrderPriceCalculationStrategy();
        var historicalData = CreateHistoricalData(s_testAsset, s_testDate, 185.92m);

        var act = () => strategy.CalculateOrderPrices(
            s_testDate, new Asset("MISSING"), TradeAction.Buy, historicalData);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*market data*asset*");
    }
}
