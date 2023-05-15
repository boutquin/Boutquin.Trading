﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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

using Boutquin.Domain.Exceptions;
using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Interfaces;
using Moq;

namespace Boutquin.Trading.UnitTests.Application;

public sealed class BuyAndHoldStrategyTests
{
    private readonly Mock<IOrderPriceCalculationStrategy> _orderPriceCalculationStrategyMock;
    private readonly Mock<IPositionSizer> _positionSizerMock;
    private readonly SortedDictionary<CurrencyCode, decimal> _cash;
    private readonly IReadOnlyDictionary<string, CurrencyCode> _assets;
    private readonly string _name;
    private readonly DateOnly _initialTimestamp;

    public BuyAndHoldStrategyTests()
    {
        _orderPriceCalculationStrategyMock = new Mock<IOrderPriceCalculationStrategy>();
        _positionSizerMock = new Mock<IPositionSizer>();
        _cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10000m } };
        _assets = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };
        _name = "TestStrategy";
        _initialTimestamp = DateOnly.FromDateTime(DateTime.Today);
    }
    [Fact]
    public void BuyAndHoldStrategy_Constructor_ValidParameters_ShouldCreateInstance()
    {
        // Arrange
        // Act
        var strategy = new BuyAndHoldStrategy(_name, _assets, _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object);

        // Assert
        strategy.Should().NotBeNull();
    }

    [Fact]
    public void BuyAndHoldStrategy_Constructor_NullOrInvalidParameters_ShouldThrowException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BuyAndHoldStrategy(null, _assets, _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object));
        Assert.Throws<EmptyOrNullDictionaryException>(() => new BuyAndHoldStrategy(_name, new Dictionary<string, CurrencyCode>(), _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object));
        Assert.Throws<EmptyOrNullDictionaryException>(() => new BuyAndHoldStrategy(_name, _assets, new SortedDictionary<CurrencyCode, decimal>(), _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object));
        Assert.Throws<ArgumentNullException>(() => new BuyAndHoldStrategy(_name, _assets, _cash, _initialTimestamp, null, _positionSizerMock.Object));
        Assert.Throws<ArgumentNullException>(() => new BuyAndHoldStrategy(_name, _assets, _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, null));
    }

    [Fact]
    public void BuyAndHoldStrategy_GenerateSignals_InitialTimestamp_ShouldGenerateBuySignals()
    {
        // Arrange
        var strategy = new BuyAndHoldStrategy(_name, _assets, _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object);
        var marketData = new MarketData(
                Timestamp: _initialTimestamp,
                Open: 100,
                High: 200,
                Low: 50,
                Close: 150,
                AdjustedClose: 150,
                Volume: 1000000,
                DividendPerShare: 0,
                SplitCoefficient: 1);

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>>
        {
            { _initialTimestamp, new SortedDictionary<string, MarketData> { { "AAPL", marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var signalEvent = strategy.GenerateSignals(_initialTimestamp, historicalMarketData, CurrencyCode.USD, historicalFxConversionRates);

        // Assert
        signalEvent.Should().NotBeNull();
        signalEvent.Timestamp.Should().Be(_initialTimestamp);
        signalEvent.StrategyName.Should().Be(_name);
        signalEvent.Signals.Should().ContainKey("AAPL");
        signalEvent.Signals["AAPL"].Should().Be(SignalType.Underweight);
    }

    [Fact]
    public void BuyAndHoldStrategy_GenerateSignals_NotInitialTimestamp_ShouldNotGenerateBuySignals()
    {
        // Arrange
        var strategy = new BuyAndHoldStrategy(_name, _assets, _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object);
        var marketData = new MarketData(
                Timestamp: _initialTimestamp.AddDays(1),
                Open: 100,
                High: 200,
                Low: 50,
                Close: 150,
                AdjustedClose: 150,
                Volume: 1000000,
                DividendPerShare: 0,
                SplitCoefficient: 1);
        
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>>
        {
            { _initialTimestamp.AddDays(1), new SortedDictionary<string, MarketData> { { "AAPL", marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp.AddDays(1), new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var signalEvent = strategy.GenerateSignals(_initialTimestamp.AddDays(1), historicalMarketData, CurrencyCode.USD, historicalFxConversionRates);

        // Assert
        signalEvent.Should().NotBeNull();
        signalEvent.Signals.Should().BeEmpty();
    }
}
