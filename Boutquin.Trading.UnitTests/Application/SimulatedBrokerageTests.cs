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
// ReSharper disable ObjectCreationAsStatement
namespace Boutquin.Trading.Tests.UnitTests.Application;

using Moq;

using Trading.Application.Brokers;
using Trading.Domain.Data;
using Trading.Domain.Enums;
using Trading.Domain.Events;
using Trading.Domain.Interfaces;

/// <summary>
/// Represents a set of tests for the SimulatedBrokerage class.
/// </summary>
public sealed class SimulatedBrokerageTests
{
    private readonly Mock<IMarketDataFetcher> _marketDataFetcherMock;
    private readonly SimulatedBrokerage _simulatedBrokerage;

    /// <summary>
    /// Initializes a new instance of the SimulatedBrokerageTests class.
    /// </summary>
    public SimulatedBrokerageTests()
    {
        _marketDataFetcherMock = new Mock<IMarketDataFetcher>();
        _simulatedBrokerage = new SimulatedBrokerage(_marketDataFetcherMock.Object);
    }

    // write a test to verify the constructor throws ArgumentNullException when the marketDataFetcher is null
    /// <summary>
    /// Tests that the constructor of the SimulatedBrokerage class throws an ArgumentNullException when the marketDataFetcher is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullMarketDataFetcher_ShouldThrowArgumentNullException()
    {
        // Act
#pragma warning disable CA1806
        Action act = () => new SimulatedBrokerage(null);
#pragma warning restore CA1806

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class returns true when given a valid market order.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_WithValidMarketOrder_ShouldReturnTrue()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var order = new Order(
            Timestamp: today, 
            StrategyName: "Strategy1", 
            Asset: "AAPL", 
            TradeAction: TradeAction.Buy, 
            OrderType: OrderType.Market, 
            Quantity: 10);
        var marketData = new SortedDictionary<string, MarketData>
        {
            {
                "AAPL", 
                new MarketData(
                    Timestamp: today, 
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class returns false when there is no market data.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_NoMarketData_ShouldReturnFalse()
    {
        // Arrange
        var order = new Order(
            Timestamp: DateOnly.FromDateTime(DateTime.Today), 
            StrategyName: "Strategy1", 
            Asset: "AAPL", 
            TradeAction: TradeAction.Buy, 
            OrderType: OrderType.Market, 
            Quantity: 10);
        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(Enumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>>().ToAsyncEnumerable());

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class returns true when given a valid limit order.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_WithValidLimitOrder_ShouldReturnTrue()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var order = new Order(
            Timestamp: today, 
            StrategyName: "Strategy1", 
            Asset: "AAPL", 
            TradeAction: TradeAction.Buy, 
            OrderType: OrderType.Limit, 
            Quantity: 10, 
            PrimaryPrice: 100);
        var marketData = new SortedDictionary<string, MarketData>
        {
            {
                "AAPL", 
                new MarketData(
                    Timestamp: today, 
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class returns false when given an invalid limit order.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_WithInValidLimitOrder_ShouldReturnFalse()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var order = new Order(
            Timestamp: today, 
            StrategyName: "Strategy1", 
            Asset: "AAPL", 
            TradeAction: TradeAction.Buy, 
            OrderType: OrderType.Limit, 
            Quantity: 10, 
            PrimaryPrice: 300);
        var marketData = new SortedDictionary<string, MarketData>
        {
            {
                "AAPL", 
                new MarketData(
                    Timestamp: today, 
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class does not trigger the FillOccurred event when given a limit order that is not better than the close price.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_LimitOrderNotBetterThanClose_ShouldNotTriggerFillEvent()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var order = new Order(
            Timestamp: today, 
            StrategyName: "Strategy1", 
            Asset: "AAPL", 
            TradeAction: TradeAction.Buy, 
            OrderType: OrderType.Limit, 
            Quantity: 10, 
            PrimaryPrice: 250);
        var marketData = new SortedDictionary<string, MarketData>
        {
            {
                "AAPL", 
                new MarketData(
                    Timestamp: today, 
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => eventTriggered = true;

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order);

        // Assert
        result.Should().BeFalse();
        eventTriggered.Should().BeFalse();
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class triggers the FillOccurred event when given a stop order that is not worse than the close price.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_StopOrderNotWorseThanClose_ShouldTriggerFillEvent()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: "AAPL",
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Stop,
            Quantity: 10,
            PrimaryPrice: 100);
        var marketData = new SortedDictionary<string, MarketData>
        {
            {
                "AAPL",
                new MarketData(
                    Timestamp: today,
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => eventTriggered = true;

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order);

        // Assert
        result.Should().BeTrue();
        eventTriggered.Should().BeTrue();
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class does not trigger the FillOccurred event when given a stop limit order that does not meet the conditions.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_StopLimitOrderNotMeetingConditions_ShouldNotTriggerFillEvent()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var order = new Order(
            Timestamp: today, 
            StrategyName: "Strategy1", 
            Asset: "AAPL", 
            TradeAction: TradeAction.Buy, 
            OrderType: OrderType.StopLimit, 
            Quantity: 10, 
            PrimaryPrice: 160, 
            SecondaryPrice: 140);
        var marketData = new SortedDictionary<string, MarketData>
        {
            {
                "AAPL", 
                new MarketData(
                    Timestamp: today, 
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => eventTriggered = true;

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order);

        // Assert
        result.Should().BeFalse();
        eventTriggered.Should().BeFalse();
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class triggers the FillOccurred event when given a stop limit order that meets the conditions.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_StopLimitOrderMeetingConditions_ShouldTriggerFillEvent()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var order = new Order(
            Timestamp: today, 
            StrategyName: "Strategy1", 
            Asset: "AAPL", 
            TradeAction: TradeAction.Buy, 
            OrderType: OrderType.StopLimit, 
            Quantity: 10, 
            PrimaryPrice: 160, 
            SecondaryPrice: 170);
        var marketData = new SortedDictionary<string, MarketData>
        {
            {
                "AAPL", 
                new MarketData(
                    Timestamp: today, 
                    Open: 100, 
                    High: 200, 
                    Low: 50, 
                    Close: 165, 
                    AdjustedClose: 165, 
                    Volume: 1000000, 
                    DividendPerShare: 0, 
                    SplitCoefficient: 1)
            }
        };
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => eventTriggered = true;

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order);

        // Assert
        result.Should().BeTrue();
        eventTriggered.Should().BeTrue();
    }
}
