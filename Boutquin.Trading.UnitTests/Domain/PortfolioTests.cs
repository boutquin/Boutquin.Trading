// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
namespace Boutquin.Trading.UnitTests.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Moq;

using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Events;
using Boutquin.Trading.Domain.Interfaces;
using Helpers;
using Boutquin.Trading.Domain.Data;

public class PortfolioTests
{
    private readonly Mock<IEventProcessor> _mockEventProcessor;
    private readonly Mock<IBrokerage> _mockBroker;
    private readonly IPortfolio _testPortfolio;

    public PortfolioTests()
    {
        _mockEventProcessor = new Mock<IEventProcessor>();
        _mockBroker = new Mock<IBrokerage>();
        _testPortfolio = new TestPortfolio
        {
            EventProcessor = _mockEventProcessor.Object,
            Broker = _mockBroker.Object
        };
    }

    [Fact]
    public async Task HandleEventAsync_ShouldCallProcessEventAsync_GivenValidEvent()
    {
        var mockEvent = new Mock<IEvent>();

        await _testPortfolio.HandleEventAsync(mockEvent.Object);

        _mockEventProcessor.Verify(x => x.ProcessEventAsync(It.IsAny<IEvent>()), Times.Once);
    }

    [Fact]
    public void UpdateCashForDividend_ShouldUpdateCash_GivenAssetWithDividend()
    {
        var asset = "AAPL";
        var dividendPerShare = 0.82m;

        var testStrategy = new TestStrategy
        {
            Positions = new SortedDictionary<string, int> { { asset, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10000m } }
        };

        ((TestPortfolio)_testPortfolio).Strategies = new Dictionary<string, IStrategy> { { "TestStrategy", testStrategy } };
        ((TestPortfolio)_testPortfolio).AssetCurrencies = new Dictionary<string, CurrencyCode> { { asset, CurrencyCode.USD } };

        _testPortfolio.UpdateCashForDividend(asset, dividendPerShare);

        testStrategy.Cash[CurrencyCode.USD].Should().Be(10082m);
    }

    [Fact]
    public async Task SubmitOrderAsync_ShouldSubmitOrder_GivenValidOrderEvent()
    {
        var orderEvent = new OrderEvent(
            DateOnly.FromDateTime(DateTime.Today),
            "TestStrategy",
            "AAPL",
            TradeAction.Buy,
            OrderType.Market,
            100
        );

        await _testPortfolio.SubmitOrderAsync(orderEvent);

        _mockBroker.Verify(x => x.SubmitOrderAsync(It.Is<Order>(o =>
            o.Timestamp == orderEvent.Timestamp &&
            o.StrategyName == orderEvent.StrategyName &&
            o.Asset == orderEvent.Asset &&
            o.TradeAction == orderEvent.TradeAction &&
            o.OrderType == orderEvent.OrderType &&
            o.Quantity == orderEvent.Quantity &&
            o.PrimaryPrice == orderEvent.PrimaryPrice &&
            o.SecondaryPrice == orderEvent.SecondaryPrice)), Times.Once);
    }
    [Fact]
    public void UpdateEquityCurve_ShouldUpdateEquity_GivenTimestampAndBaseCurrency()
    {
        IStrategy strategy = new TestStrategy();
        var timestamp = DateOnly.FromDateTime(DateTime.Today);
        var baseCurrency = CurrencyCode.USD;
        var marketData = new SortedDictionary<string, MarketData>
        {
            { "AAPL",
                new MarketData(
                    Timestamp: timestamp,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000,
                    DividendPerShare: 0,
                    SplitCoefficient: 1)
            }
        };
        var fxRates = new SortedDictionary<CurrencyCode, decimal>
        {
            { CurrencyCode.EUR, 0.85m }
        };

        strategy.Positions["AAPL"] = 10;
        ((TestStrategy)strategy).Assets = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };
        strategy.Cash[CurrencyCode.USD] = 1000;

        ((TestPortfolio)_testPortfolio).HistoricalMarketData = new SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> { { timestamp, marketData } };
        ((TestPortfolio)_testPortfolio).HistoricalFxConversionRates = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> { { timestamp, fxRates } };
        ((TestPortfolio)_testPortfolio).Strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };

        _testPortfolio.UpdateEquityCurve(timestamp, baseCurrency);

        _testPortfolio.EquityCurve[timestamp].Should().NotBe(0);
    }
}
