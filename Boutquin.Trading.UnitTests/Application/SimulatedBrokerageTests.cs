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
        Action act = () => new SimulatedBrokerage(null);

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
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

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
            Timestamp: new DateOnly(2024, 1, 15),
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);
        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(Enumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>().ToAsyncEnumerable());

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

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
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Limit,
            Quantity: 10,
            PrimaryPrice: 100);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

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
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Limit,
            Quantity: 10,
            PrimaryPrice: 40);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

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
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Limit,
            Quantity: 10,
            PrimaryPrice: 40);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeFalse();
        eventTriggered.Should().BeFalse();
    }

    /// <summary>
    /// H5: Verifies that a pre-cancelled CancellationToken causes SubmitOrderAsync to throw OperationCanceledException.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_CancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        // Act
        var act = () => _simulatedBrokerage.SubmitOrderAsync(order, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }

    /// <summary>
    /// M4: Buy stop order should trigger when the daily High reaches the stop price.
    /// </summary>
    [Fact]
    public async Task HandleStopOrder_BuyStop_ShouldTriggerOnHighPrice()
    {
        // Arrange — stop at 105, High=106 (reaches stop), Close=100 (below stop)
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Stop,
            Quantity: 10,
            PrimaryPrice: 105);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
                new MarketData(
                    Timestamp: today,
                    Open: 100,
                    High: 106,
                    Low: 98,
                    Close: 100,
                    AdjustedClose: 100,
                    Volume: 1000000,
                    DividendPerShare: 0,
                    SplitCoefficient: 1)
            }
        };

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData) }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

        // Assert — High (106) >= stopPrice (105), so the stop triggers
        result.Should().BeTrue();
        eventTriggered.Should().BeTrue();
    }

    /// <summary>
    /// M4: Sell stop order should trigger when the daily Low reaches the stop price.
    /// </summary>
    [Fact]
    public async Task HandleStopOrder_SellStop_ShouldTriggerOnLowPrice()
    {
        // Arrange — stop at 95, Low=94 (reaches stop), Close=100 (above stop)
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Sell,
            OrderType: OrderType.Stop,
            Quantity: 10,
            PrimaryPrice: 95);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
                new MarketData(
                    Timestamp: today,
                    Open: 100,
                    High: 106,
                    Low: 94,
                    Close: 100,
                    AdjustedClose: 100,
                    Volume: 1000000,
                    DividendPerShare: 0,
                    SplitCoefficient: 1)
            }
        };

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData) }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

        // Assert — Low (94) <= stopPrice (95), so the stop triggers
        result.Should().BeTrue();
        eventTriggered.Should().BeTrue();
    }

    /// <summary>
    /// M5: FillOccurred delegate should be invoked via thread-safe snapshot pattern.
    /// </summary>
    [Fact]
    public async Task FillOccurred_SubscribedHandler_ShouldBeInvoked()
    {
        // Arrange
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
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

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData) }.ToAsyncEnumerable());

        FillEvent? capturedFill = null;
        _simulatedBrokerage.FillOccurred += (sender, fill) =>
        {
            capturedFill = fill;
            return Task.CompletedTask;
        };

        // Act
        await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

        // Assert
        capturedFill.Should().NotBeNull();
        capturedFill!.Asset.Should().Be(new Asset("AAPL"));
        capturedFill.Quantity.Should().Be(10);
    }

    /// <summary>
    /// Tests that the SubmitOrderAsync method of the SimulatedBrokerage class triggers the FillOccurred event when given a stop order that is not worse than the close price.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_StopOrderNotWorseThanClose_ShouldTriggerFillEvent()
    {
        // Arrange
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Stop,
            Quantity: 10,
            PrimaryPrice: 100);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

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
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.StopLimit,
            Quantity: 10,
            PrimaryPrice: 160,
            SecondaryPrice: 140);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

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
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Asset: new Asset("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.StopLimit,
            Quantity: 10,
            PrimaryPrice: 160,
            SecondaryPrice: 170);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
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
        var marketDataKeyValuePair = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(today, marketData);

        _marketDataFetcherMock.Setup(mdf => mdf.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { marketDataKeyValuePair }.ToAsyncEnumerable());

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        eventTriggered.Should().BeTrue();
    }
}
