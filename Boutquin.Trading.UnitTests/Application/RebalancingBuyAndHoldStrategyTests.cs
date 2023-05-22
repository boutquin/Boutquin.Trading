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
namespace Boutquin.Trading.UnitTests.Application;

using Moq;

using Trading.Application.Strategies;
using Trading.Domain.Data;
using Trading.Domain.Enums;
using Trading.Domain.Interfaces;

public sealed class RebalancingBuyAndHoldStrategyTests
{
    private readonly string _name = "TestStrategy";
    private readonly IReadOnlyDictionary<string, CurrencyCode> _assets = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };
    private readonly SortedDictionary<CurrencyCode, decimal> _cash = new() { { CurrencyCode.USD, 10000m } };
    private readonly Mock<IOrderPriceCalculationStrategy> _orderPriceCalculationStrategyMock = new();
    private readonly Mock<IPositionSizer> _positionSizerMock = new();
    private readonly DateOnly _initialTimestamp = new(year: 2023, month: 5, day: 1);
    private readonly MarketData _marketData = new(
        Timestamp: new DateOnly(year: 2023, month: 5, day: 1),
        Open: 100,
        High: 200,
        Low: 50,
        Close: 150,
        AdjustedClose: 150,
        Volume: 1000000,
        DividendPerShare: 0,
        SplitCoefficient: 1);

    [Fact]
    public void RebalancingBuyAndHoldStrategy_Constructor_ShouldCreateInstance()
    {
        // Act
        var strategy = new RebalancingBuyAndHoldStrategy(_name, _assets, _cash, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object, RebalancingFrequency.Daily);

        // Assert
        strategy.Should().NotBeNull();
        strategy.Name.Should().Be(_name);
        strategy.Assets.Should().BeEquivalentTo(_assets);
        strategy.Cash.Should().BeEquivalentTo(_cash);
        strategy.OrderPriceCalculationStrategy.Should().Be(_orderPriceCalculationStrategyMock.Object);
        strategy.PositionSizer.Should().Be(_positionSizerMock.Object);
    }

    [Fact]
    public void RebalancingBuyAndHoldStrategy_GenerateSignals_RebalancingDate_ShouldGenerateRebalanceSignals()
    {
        // Arrange
        var strategy = new RebalancingBuyAndHoldStrategy(_name, _assets, _cash, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object, RebalancingFrequency.Daily);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>?>
        {
            { _initialTimestamp, new SortedDictionary<string, MarketData> { { "AAPL", _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var signalEvent = strategy.GenerateSignals(
            _initialTimestamp,
            CurrencyCode.USD,
            historicalMarketData, 
            historicalFxConversionRates);

        // Assert
        signalEvent.Should().NotBeNull();
        signalEvent.Timestamp.Should().Be(_initialTimestamp);
        signalEvent.StrategyName.Should().Be(_name);
        signalEvent.Signals.Should().ContainKey("AAPL");
        signalEvent.Signals["AAPL"].Should().Be(SignalType.Rebalance);
    }

    [Fact]
    public void RebalancingBuyAndHoldStrategy_GenerateSignals_NotRebalancingDate_ShouldNotGenerateRebalanceSignals()
    {
        // Arrange
        var strategy = new RebalancingBuyAndHoldStrategy(_name, _assets, _cash, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object, RebalancingFrequency.Monthly);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>?>
        {
            { _initialTimestamp, new SortedDictionary<string, MarketData> { { "AAPL", _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        _ = strategy.GenerateSignals(
            _initialTimestamp,
            CurrencyCode.USD,
            historicalMarketData, 
            historicalFxConversionRates);
        var signalEvent = strategy.GenerateSignals(
            _initialTimestamp.AddDays(15),
            CurrencyCode.USD,
            historicalMarketData, 
            historicalFxConversionRates);

        // Assert
        signalEvent.Should().NotBeNull();
        signalEvent.Timestamp.Should().Be(_initialTimestamp.AddDays(15));
        signalEvent.StrategyName.Should().Be(_name);
        signalEvent.Signals.Should().BeEmpty();  // No rebalancing signals should be generated
    }

    [Fact]
    public void RebalancingBuyAndHoldStrategy_GenerateSignals_SecondRebalancingDate_ShouldGenerateRebalanceSignals()
    {
        // Arrange
        var strategy = new RebalancingBuyAndHoldStrategy(_name, _assets, _cash, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object, RebalancingFrequency.Monthly);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>?>
        {
            { _initialTimestamp, new SortedDictionary<string, MarketData> { { "AAPL", _marketData } } },
            { _initialTimestamp.AddMonths(1), new SortedDictionary<string, MarketData> { { "AAPL", _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } },
            { _initialTimestamp.AddMonths(1), new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var firstSignalEvent = strategy.GenerateSignals(
            _initialTimestamp,
            CurrencyCode.USD,
            historicalMarketData, 
            historicalFxConversionRates);
        var secondSignalEvent = strategy.GenerateSignals(
            _initialTimestamp.AddMonths(1),
            CurrencyCode.USD,
            historicalMarketData, 
            historicalFxConversionRates);

        // Assert
        firstSignalEvent.Should().NotBeNull();
        firstSignalEvent.Timestamp.Should().Be(_initialTimestamp);
        firstSignalEvent.StrategyName.Should().Be(_name);
        firstSignalEvent.Signals.Should().ContainKey("AAPL");
        firstSignalEvent.Signals["AAPL"].Should().Be(SignalType.Rebalance);

        secondSignalEvent.Should().NotBeNull();
        secondSignalEvent.Timestamp.Should().Be(_initialTimestamp.AddMonths(1));
        secondSignalEvent.StrategyName.Should().Be(_name);
        secondSignalEvent.Signals.Should().ContainKey("AAPL");
        secondSignalEvent.Signals["AAPL"].Should().Be(SignalType.Rebalance);
    }
}
