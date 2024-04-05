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

using Boutquin.Domain.Exceptions;

using Moq;

using Trading.Application.Strategies;
using Trading.Domain.Data;
using Trading.Domain.Enums;
using Trading.Domain.Interfaces;

/// <summary>
/// Represents a set of tests for the BuyAndHoldStrategy class.
/// </summary>
public sealed class BuyAndHoldStrategyTests
{
    private readonly Mock<IOrderPriceCalculationStrategy> _orderPriceCalculationStrategyMock = new();
    private readonly Mock<IPositionSizer> _positionSizerMock = new();
    private readonly SortedDictionary<CurrencyCode, decimal> _cash = new() { { CurrencyCode.USD, 10000m } };
    private readonly IReadOnlyDictionary<string, CurrencyCode> _assets = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };
    private readonly string _name = "TestStrategy";
    private readonly DateOnly _initialTimestamp = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// Tests that the BuyAndHoldStrategy constructor creates an instance when given valid parameters.
    /// </summary>
    [Fact]
    public void BuyAndHoldStrategy_Constructor_ValidParameters_ShouldCreateInstance()
    {
        // Arrange
        // Act
        var strategy = new BuyAndHoldStrategy(_name, _assets, _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object);

        // Assert
        strategy.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that the BuyAndHoldStrategy constructor throws an exception when given null or invalid parameters.
    /// </summary>
    [Fact]
    public void BuyAndHoldStrategy_Constructor_NullOrInvalidParameters_ShouldThrowException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BuyAndHoldStrategy(null, _assets, _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object));
        Assert.Throws<EmptyOrNullDictionaryException>(() => new BuyAndHoldStrategy(_name, new Dictionary<string, CurrencyCode>(), _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object));
        Assert.Throws<EmptyOrNullDictionaryException>(() => new BuyAndHoldStrategy(_name, _assets, [], _initialTimestamp, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object));
        Assert.Throws<ArgumentNullException>(() => new BuyAndHoldStrategy(_name, _assets, _cash, _initialTimestamp, null, _positionSizerMock.Object));
        Assert.Throws<ArgumentNullException>(() => new BuyAndHoldStrategy(_name, _assets, _cash, _initialTimestamp, _orderPriceCalculationStrategyMock.Object, null));
    }

    /// <summary>
    /// Tests that the GenerateSignals method of the BuyAndHoldStrategy class generates buy signals at the initial timestamp.
    /// </summary>
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

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>?>
                    {
                        { _initialTimestamp, new SortedDictionary<string, MarketData> { { "AAPL", marketData } } }
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
        signalEvent.Signals["AAPL"].Should().Be(SignalType.Underweight);
    }

    /// <summary>
    /// Tests that the GenerateSignals method of the BuyAndHoldStrategy class does not generate buy signals at timestamps other than the initial timestamp.
    /// </summary>
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

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>?>
                    {
                        { _initialTimestamp.AddDays(1), new SortedDictionary<string, MarketData> { { "AAPL", marketData } } }
                    };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
                    {
                        { _initialTimestamp.AddDays(1), new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
                    };

        // Act
        var signalEvent = strategy.GenerateSignals(
            _initialTimestamp.AddDays(1),
            CurrencyCode.USD,
            historicalMarketData,
            historicalFxConversionRates);

        // Assert
        signalEvent.Should().NotBeNull();
        signalEvent.Signals.Should().BeEmpty();
    }
}
