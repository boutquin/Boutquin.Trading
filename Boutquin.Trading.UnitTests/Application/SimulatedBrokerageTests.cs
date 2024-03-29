﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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

using Moq;

using Trading.Application.Brokers;
using Trading.Domain.Data;
using Trading.Domain.Enums;
using Trading.Domain.Events;
using Trading.Domain.Interfaces;

public sealed class SimulatedBrokerageTests
{
    private readonly Mock<IMarketDataFetcher> _marketDataFetcherMock;
    private readonly SimulatedBrokerage _simulatedBrokerage;

    public SimulatedBrokerageTests()
    {
        _marketDataFetcherMock = new Mock<IMarketDataFetcher>();
        _simulatedBrokerage = new SimulatedBrokerage(_marketDataFetcherMock.Object);
    }

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
