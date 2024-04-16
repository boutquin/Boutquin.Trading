// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Tests.UnitTests.Application;

using System.Collections.ObjectModel;

using Helpers;

using Moq;

using Trading.Application;
using Trading.Application.EventHandlers;
using Trading.Domain.Data;
using Trading.Domain.Enums;
using Trading.Domain.Events;
using Trading.Domain.Interfaces;

/// <summary>
/// Represents a set of tests for the Portfolio class.
/// </summary>
public class PortfolioTests
{
    private readonly Mock<IEventProcessor> _mockEventProcessor = new();
    private readonly Mock<IBrokerage> _mockBroker = new();
    private readonly Dictionary<Type, IEventHandler> _handlers = new() 
    {
        { typeof(OrderEvent), new OrderEventHandler() },
        { typeof(MarketEvent), new MarketEventHandler() },
        { typeof(FillEvent), new FillEventHandler() },
        { typeof(SignalEvent), new SignalEventHandler() }
    };

    /// <summary>
    /// Tests that the HandleEventAsync method of the Portfolio class calls the ProcessEventAsync method of the IEventProcessor interface when given a valid event.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_ShouldCallProcessEventAsync_GivenValidEvent()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var mockEvent = new Mock<IFinancialEvent>();

        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        await portfolio.HandleEventAsync(mockEvent.Object);

        // Assert
        _mockEventProcessor.Verify(x => x.ProcessEventAsync(It.IsAny<IFinancialEvent>()), Times.Once);
    }

    /// <summary>
    /// Tests that the UpdateHistoricalData method of the Portfolio class updates the historical data correctly when given a valid market event.
    /// </summary>
    [Fact]
    public void UpdateHistoricalData_ShouldUpdateHistoricalData_GivenValidMarketEvent()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var timestamp = DateOnly.FromDateTime(DateTime.Today);
        var marketEvent = new MarketEvent(Timestamp: timestamp,
                                            HistoricalMarketData: [],
                                            HistoricalFxConversionRates: []);

        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        portfolio.UpdateHistoricalData(marketEvent);

        // Assert
        portfolio.HistoricalMarketData.Should().ContainKey(timestamp);
        portfolio.HistoricalFxConversionRates.Should().ContainKey(timestamp);
    }

    /// <summary>
    /// Tests that the UpdateCashForDividend method of the Portfolio class updates the cash correctly when given an asset with a dividend.
    /// </summary>
    [Fact]
    public void UpdateCashForDividend_ShouldUpdateCash_GivenAssetWithDividend()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var asset = "AAPL";
        var dividendPerShare = 0.82m;

        var testStrategy = new TestStrategy
        {
            Positions = new SortedDictionary<string, int> { { asset, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10000m } }
        };
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", testStrategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { asset, CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        portfolio.UpdateCashForDividend(asset, dividendPerShare);

        // Assert
        testStrategy.Cash[CurrencyCode.USD].Should().Be(10082m);
    }

    /// <summary>
    /// Tests that the UpdateCashForDividend method of the Portfolio class updates the cash for a dividend correctly when given an asset and a dividend per share.
    /// </summary>
    [Fact]
    public void UpdateCashForDividend_ShouldUpdateCashForDividend_GivenAssetAndDividendPerShare()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var asset = "AAPL";
        var dividendPerShare = 2m;
        var quantity = 10;

        var testStrategy = new TestStrategy
        {
            Positions = new SortedDictionary<string, int> { { asset, quantity } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1000m } }
        };
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", testStrategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { asset, CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        portfolio.UpdateCashForDividend(asset, dividendPerShare);
        
        // Assert
        testStrategy.Cash[CurrencyCode.USD].Should().Be(1020m);
    }

    /// <summary>
    /// Tests that the GenerateSignals method of the Portfolio class returns the correct signals when given a valid market event and a base currency.
    /// </summary>
    [Fact]
    public void GenerateSignals_ShouldReturnSignals_GivenValidMarketEventAndBaseCurrency()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var timestamp = DateOnly.FromDateTime(DateTime.Today);
        var marketEvent = new MarketEvent(Timestamp: timestamp,
            HistoricalMarketData: [],
            HistoricalFxConversionRates: []);

        var expectedSignal = new SignalEvent(timestamp, "TestStrategy", new Dictionary<string, SignalType>
        {
            { "AAPL", SignalType.Underweight },
            { "GOOG", SignalType.Overweight }
        });

        var mockStrategy = new Mock<IStrategy>();
        mockStrategy.Setup(s => s.GenerateSignals(
            It.IsAny<DateOnly>(),
            It.IsAny<CurrencyCode>(),
            It.IsAny<SortedDictionary<DateOnly, SortedDictionary<string, MarketData>?>>(),
            It.IsAny<SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>()
        )).Returns(expectedSignal);

        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", mockStrategy.Object } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        var signals = portfolio.GenerateSignals(marketEvent).ToList();

        // Assert  
        signals.Should().HaveCount(1);
        signals[0].Should().BeEquivalentTo(expectedSignal);
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the Portfolio class submits an order correctly when given a valid order event.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_ShouldSubmitOrder_GivenValidOrderEvent()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var orderEvent = new OrderEvent(
            DateOnly.FromDateTime(DateTime.Today),
            "TestStrategy",
            "AAPL",
            TradeAction.Buy,
            OrderType.Market,
            100
        );

        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        await portfolio.SubmitOrderAsync(orderEvent);

        // Assert
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

    /// <summary>
    /// Tests that the FillOccurred event of the IBrokerage interface calls the HandleEventAsync method of the Portfolio class.
    /// </summary>
    [Fact]
    public async Task Broker_FillOccurred_ShouldCallHandleEventAsync()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        var fillEvent = new FillEvent(
            Timestamp: DateOnly.FromDateTime(DateTime.Today),
            Asset: "AAPL",
            StrategyName: "TestStrategy",
            FillPrice: 150m,
            Quantity: 10,
            Commission: 1m
        );
        _mockEventProcessor.Setup(x => x.ProcessEventAsync(It.IsAny<IFinancialEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        _mockBroker.Raise(broker => broker.FillOccurred += null, this, fillEvent);

        // Assert
        _mockEventProcessor.Verify(x => x.ProcessEventAsync(It.Is<IFinancialEvent>(e => e == fillEvent)), Times.Once);
    }

    /// <summary>
    /// Tests that the UpdatePosition method of the Portfolio class updates a position correctly when given a valid strategy name, asset, and quantity.
    /// </summary>
    [Fact]
    public void UpdatePosition_ShouldUpdatePosition_GivenValidStrategyNameAssetAndQuantity()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var strategyName = "TestStrategy";
        var asset = "AAPL";
        var quantity = 10;

        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        portfolio.UpdatePosition(strategyName, asset, quantity);

        // Assert
        strategy.Positions[asset].Should().Be(quantity);
    }

    /// <summary>
    /// Tests that the UpdateCash method of the Portfolio class updates the cash correctly when given a valid strategy name, currency, and amount.
    /// </summary>
    [Fact]
    public void UpdateCash_ShouldUpdateCash_GivenValidStrategyNameCurrencyAndAmount()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var strategyName = "TestStrategy";
        var currency = CurrencyCode.USD;
        var amount = 1000m;

        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        portfolio.UpdateCash(strategyName, currency, amount);

        // Assert
        strategy.Cash[currency].Should().Be(amount);
    }

    /// <summary>
    /// Tests that the AdjustPositionForSplit method of the Portfolio class adjusts a position correctly when given an asset and a split ratio.
    /// </summary>
    [Fact]
    public void AdjustPositionForSplit_ShouldAdjustPosition_GivenAssetAndSplitRatio()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var asset = "AAPL";
        var splitRatio = 2m;

        IStrategy strategy = new TestStrategy();
        strategy.Positions[asset] = 10;

        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        portfolio.AdjustPositionForSplit(asset, splitRatio);

        // Assert
        strategy.Positions[asset].Should().Be(20);
    }

    /// <summary>
    /// Tests that the AdjustHistoricalDataForSplit method of the Portfolio class adjusts the historical data correctly when given an asset and a split ratio.
    /// </summary>
    [Fact]
    public void AdjustHistoricalDataForSplit_ShouldAdjustHistoricalData_GivenAssetAndSplitRatio()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var asset = "AAPL";
        var timestamp = DateOnly.FromDateTime(DateTime.Today);
        var splitRatio = 2m;
        var marketData = new SortedDictionary<string, MarketData>
        {
            {
                asset, new MarketData(
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

        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false)
        {
            HistoricalMarketData = { [timestamp] = marketData }
        };

        // Act
        portfolio.AdjustHistoricalDataForSplit(asset, splitRatio);

        // Assert
        var adjustedData = portfolio.HistoricalMarketData[timestamp][asset];
        adjustedData.Open.Should().Be(50);
        adjustedData.High.Should().Be(100);
        adjustedData.Low.Should().Be(25);
        adjustedData.Close.Should().Be(75);
        adjustedData.AdjustedClose.Should().Be(75);
        adjustedData.Volume.Should().Be(2000000);
    }

    /// <summary>
    /// Tests that the GetStrategy method of the Portfolio class returns the correct strategy when given a valid strategy name.
    /// </summary>
    [Fact]
    public void GetStrategy_ShouldReturnStrategy_GivenValidStrategyName()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var strategyName = "TestStrategy";
        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { strategyName, strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        var result = portfolio.GetStrategy(strategyName);

        // Assert
        result.Should().Be(strategy);
    }

    /// <summary>
    /// Tests that the GetAssetCurrency method of the Portfolio class returns the correct currency when given a valid asset.
    /// </summary>
    [Fact]
    public void GetAssetCurrency_ShouldReturnCurrency_GivenValidAsset()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var asset = "AAPL";
        var currency = CurrencyCode.USD;

        IStrategy strategy = new TestStrategy();
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { asset, currency } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        var result = portfolio.GetAssetCurrency(asset);

        // Assert
        result.Should().Be(currency);
    }

    /// <summary>
    /// Tests that the UpdateEquityCurve method of the Portfolio class updates the equity correctly when given a timestamp and a base currency.
    /// </summary>
    [Fact]
    public void UpdateEquityCurve_ShouldUpdateEquity_GivenTimestampAndBaseCurrency()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
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
        var fxRates = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.EUR, 0.85m } };

        strategy.Positions["AAPL"] = 10;
        ((TestStrategy)strategy).Assets = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };
        strategy.Cash[CurrencyCode.USD] = 1000;

        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", strategy } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false)
        {
            HistoricalMarketData = { [timestamp] = marketData }, 
            HistoricalFxConversionRates = { [timestamp] = fxRates }
        };

        // Act
        portfolio.UpdateEquityCurve(timestamp);

        // Assert
        portfolio.EquityCurve[timestamp].Should().Be(2500m);
    }

    /// <summary>
    /// Tests that the CalculateTotalPortfolioValue method of the Portfolio class returns the correct total value when given a timestamp and a base currency.
    /// </summary>
    [Fact]
    public void CalculateTotalPortfolioValue_ShouldReturnTotalValue_GivenTimestampAndBaseCurrency()
    {
        // Arrange
        const CurrencyCode BaseCurrency = CurrencyCode.USD;
        var timestamp = DateOnly.FromDateTime(DateTime.Today);

        var mockStrategy = new Mock<IStrategy>();
        mockStrategy.Setup(s => s.ComputeTotalValue(
            It.IsAny<DateOnly>(),
            It.IsAny<CurrencyCode>(),
            It.IsAny<SortedDictionary<DateOnly, SortedDictionary<string, MarketData>?>>(),
            It.IsAny<SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>()
        )).Returns(1000m);

        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", mockStrategy.Object } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var portfolio = new Portfolio(
            BaseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            isLive: false
        );

        // Act
        var result = portfolio.CalculateTotalPortfolioValue(timestamp);

        // Assert
        result.Should().Be(1000m);
    }
}
