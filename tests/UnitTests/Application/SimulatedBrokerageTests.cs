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
    private readonly SimulatedBrokerage _simulatedBrokerage;

    /// <summary>
    /// Initializes a new instance of the SimulatedBrokerageTests class.
    /// </summary>
    public SimulatedBrokerageTests()
    {
        _simulatedBrokerage = new SimulatedBrokerage();
    }

    /// <summary>
    /// Tests that the constructor of the SimulatedBrokerage class throws an ArgumentNullException when the cost model is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullCostModel_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new SimulatedBrokerage(null!);

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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000)
            }
        };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

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
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act — SubmitOrderAsync now always returns true (queuing succeeds)
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        // ProcessPendingOrdersAsync with empty data — no fill should occur
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, new SortedDictionary<Symbol, Bar>(), CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        eventTriggered.Should().BeFalse();
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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Limit,
            Quantity: 10,
            PrimaryPrice: 100);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000)
            }
        };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Limit,
            Quantity: 10,
            PrimaryPrice: 40);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act — SubmitOrderAsync now always returns true (queuing succeeds)
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        eventTriggered.Should().BeFalse();
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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Limit,
            Quantity: 10,
            PrimaryPrice: 40);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act — SubmitOrderAsync now always returns true (queuing succeeds)
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
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
            Symbol: new Symbol("AAPL"),
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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Stop,
            Quantity: 10,
            PrimaryPrice: 105);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 106,
                    Low: 98,
                    Close: 100,
                    AdjustedClose: 100,
                    Volume: 1000000)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Sell,
            OrderType: OrderType.Stop,
            Quantity: 10,
            PrimaryPrice: 95);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 106,
                    Low: 94,
                    Close: 100,
                    AdjustedClose: 100,
                    Volume: 1000000)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000)
            }
        };

        FillEvent? capturedFill = null;
        _simulatedBrokerage.FillOccurred += (sender, fill) =>
        {
            capturedFill = fill;
            return Task.CompletedTask;
        };

        // Act
        await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert
        capturedFill.Should().NotBeNull();
        capturedFill!.Symbol.Should().Be(new Symbol("AAPL"));
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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Stop,
            Quantity: 10,
            PrimaryPrice: 100);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.StopLimit,
            Quantity: 10,
            PrimaryPrice: 160,
            SecondaryPrice: 140);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act — SubmitOrderAsync now always returns true (queuing succeeds)
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
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
            Symbol: new Symbol("AAPL"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.StopLimit,
            Quantity: 10,
            PrimaryPrice: 160,
            SecondaryPrice: 170);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("AAPL"),
                new Bar(
                    Date: today,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 165,
                    AdjustedClose: 165,
                    Volume: 1000000)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        var result = await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeTrue();
        eventTriggered.Should().BeTrue();
    }

    /// <summary>
    /// Market orders on dividend-adjusted ETFs must fill at adjusted open, not raw open.
    /// When position sizing uses AdjustedClose and the broker fills at raw Open, the fill
    /// price is ~4-5x higher than expected, leaving the position severely underweight.
    /// This reproduces the SCHD 2012 scenario: raw ~$29, adjusted ~$6.22 (14 years of dividends).
    /// </summary>
    [Fact]
    public async Task MarketOrder_FillsAtAdjustedOpen_WhenAdjustedCloseIsLowerThanClose()
    {
        // Arrange — simulate SCHD circa 2012: raw ~$29, adj ~$6.22 (adj factor ~0.2157)
        var today = new DateOnly(2024, 1, 15);
        const decimal rawOpen = 28.84m;
        const decimal rawClose = 28.82m;
        const decimal adjClose = 6.22m;
        var expectedAdjOpen = rawOpen * (adjClose / rawClose); // ≈ 6.2244

        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Symbol: new Symbol("SCHD"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 4209);

        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("SCHD"),
                new Bar(
                    Date: today,
                    Open: rawOpen,
                    High: 29.0m,
                    Low: 28.6m,
                    Close: rawClose,
                    AdjustedClose: adjClose,
                    Volume: 168_100)
            }
        };

        FillEvent? capturedFill = null;
        _simulatedBrokerage.FillOccurred += (sender, fill) =>
        {
            capturedFill = fill;
            return Task.CompletedTask;
        };

        // Act
        await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert — fill price must be the adjusted open, not the raw open ($28.84)
        capturedFill.Should().NotBeNull();
        capturedFill!.FillPrice.Should().BeApproximately(expectedAdjOpen, precision: 0.001m,
            because: "market order fill price must use AdjustedOpen = Open × (AdjustedClose / Close) " +
                     "so that position sizing (which uses AdjustedClose) and fill prices are consistent");
    }

    /// <summary>
    /// Limit buy orders on dividend-adjusted ETFs must compare the fill condition against
    /// adjusted low, not raw low. A limit price derived from AdjustedClose (~$6.22) would
    /// never fill against raw Low (~$28.65), leaving rebalance orders permanently unfilled.
    /// </summary>
    [Fact]
    public async Task LimitBuyOrder_FillConditionUsesAdjustedLow_WhenAdjustedCloseIsLowerThanClose()
    {
        // Arrange — same SCHD 2012 scenario: limit price = AdjustedClose
        var today = new DateOnly(2024, 1, 15);
        const decimal rawOpen = 28.84m;
        const decimal rawHigh = 29.0m;
        const decimal rawLow = 28.6m;
        const decimal rawClose = 28.82m;
        const decimal adjClose = 6.22m;
        var limitPrice = adjClose; // position sizer targets this price
        var adjFactor = adjClose / rawClose;
        var expectedAdjLow = rawLow * adjFactor; // ≈ 6.175 — below limitPrice, so order SHOULD fill

        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Symbol: new Symbol("SCHD"),
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Limit,
            Quantity: 100,
            PrimaryPrice: limitPrice);

        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("SCHD"),
                new Bar(
                    Date: today,
                    Open: rawOpen,
                    High: rawHigh,
                    Low: rawLow,
                    Close: rawClose,
                    AdjustedClose: adjClose,
                    Volume: 168_100)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert — adjusted low (~$6.175) is below limit (~$6.22), so the order must fill
        eventTriggered.Should().BeTrue(
            because: $"adjusted low ({expectedAdjLow:F4}) <= limit ({limitPrice}) so the limit buy should fill; " +
                     "raw low ({rawLow}) > limit would incorrectly block the fill");
    }

    /// <summary>
    /// Limit sell orders on dividend-adjusted ETFs must compare the fill condition against
    /// adjusted high, not raw high. A limit sell at AdjustedClose would never trigger
    /// against raw High (which is ~4-5x larger), leaving circuit-breaker sells permanently queued.
    /// </summary>
    [Fact]
    public async Task LimitSellOrder_FillConditionUsesAdjustedHigh_WhenAdjustedCloseIsLowerThanClose()
    {
        // Arrange
        var today = new DateOnly(2024, 1, 15);
        const decimal rawOpen = 28.84m;
        const decimal rawHigh = 29.0m;
        const decimal rawLow = 28.6m;
        const decimal rawClose = 28.82m;
        const decimal adjClose = 6.22m;
        var limitPrice = adjClose; // sell at current adjusted close
        var adjFactor = adjClose / rawClose;
        var expectedAdjHigh = rawHigh * adjFactor; // ≈ 6.258 — above limitPrice, so order SHOULD fill

        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Symbol: new Symbol("SCHD"),
            TradeAction: TradeAction.Sell,
            OrderType: OrderType.Limit,
            Quantity: 830,
            PrimaryPrice: limitPrice);

        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                new Symbol("SCHD"),
                new Bar(
                    Date: today,
                    Open: rawOpen,
                    High: rawHigh,
                    Low: rawLow,
                    Close: rawClose,
                    AdjustedClose: adjClose,
                    Volume: 168_100)
            }
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (sender, args) => { eventTriggered = true; return Task.CompletedTask; };

        // Act
        await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert — adjusted high (~$6.258) >= limit (~$6.22), so the sell must fill
        eventTriggered.Should().BeTrue(
            because: $"adjusted high ({expectedAdjHigh:F4}) >= limit ({limitPrice}) so the limit sell should fill");
    }

    // SCHD-2012-style bar: raw prices ~$29, AdjustedClose $6.22 (factor ≈ 0.2158).
    // Adjusted range: Low ≈ 6.1725, High ≈ 6.2588, Close = 6.22.
    private const decimal AdjRawHigh = 29.0m;
    private const decimal AdjRawLow = 28.6m;
    private const decimal AdjRawClose = 28.82m;
    private const decimal AdjAdjustedClose = 6.22m;

    private async Task<FillEvent?> FillOnAdjustedBarAsync(
        TradeAction action,
        OrderType orderType,
        decimal primaryPrice,
        decimal? secondaryPrice = null)
    {
        var today = new DateOnly(2024, 1, 15);
        var symbol = new Symbol("SCHD");
        var order = new Order(
            Timestamp: today,
            StrategyName: "Strategy1",
            Symbol: symbol,
            TradeAction: action,
            OrderType: orderType,
            Quantity: 100,
            PrimaryPrice: primaryPrice,
            SecondaryPrice: secondaryPrice);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            {
                symbol,
                new Bar(
                    Date: today,
                    Open: 28.84m,
                    High: AdjRawHigh,
                    Low: AdjRawLow,
                    Close: AdjRawClose,
                    AdjustedClose: AdjAdjustedClose,
                    Volume: 168_100)
            },
        };

        FillEvent? captured = null;
        _simulatedBrokerage.FillOccurred += (_, fill) =>
        {
            captured = fill;
            return Task.CompletedTask;
        };

        await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);
        return captured;
    }

    /// <summary>
    /// R6-F4(b): a buy stop priced on the AdjustedClose basis above the bar's adjusted High
    /// (≈ 6.26) must not trigger, even though the raw High (29) is above it.
    /// </summary>
    [Fact]
    public async Task BuyStop_AboveAdjustedHigh_DoesNotTrigger_WhenAdjustedCloseDiffersFromClose()
    {
        var fill = await FillOnAdjustedBarAsync(TradeAction.Buy, OrderType.Stop, primaryPrice: 10m).ConfigureAwait(false);

        fill.Should().BeNull(because: "the stop (10) is above the adjusted High (≈6.26); raw High (29) is on a different basis");
    }

    /// <summary>
    /// R6-F4(b): a sell stop inside the bar's adjusted range must trigger, even though the raw
    /// Low (28.6) is far above it.
    /// </summary>
    [Fact]
    public async Task SellStop_WithinAdjustedRange_Triggers_WhenAdjustedCloseDiffersFromClose()
    {
        var fill = await FillOnAdjustedBarAsync(TradeAction.Sell, OrderType.Stop, primaryPrice: 6.20m).ConfigureAwait(false);

        fill.Should().NotBeNull(because: "the adjusted Low (≈6.17) reaches the stop (6.20)");
        fill!.FillPrice.Should().Be(6.20m);
    }

    /// <summary>
    /// R6-F4(b): a buy stop inside the bar's adjusted range still triggers and fills at the stop price.
    /// </summary>
    [Fact]
    public async Task BuyStop_WithinAdjustedRange_Triggers_WhenAdjustedCloseDiffersFromClose()
    {
        var fill = await FillOnAdjustedBarAsync(TradeAction.Buy, OrderType.Stop, primaryPrice: 6.24m).ConfigureAwait(false);

        fill.Should().NotBeNull(because: "the adjusted High (≈6.26) reaches the stop (6.24)");
        fill!.FillPrice.Should().Be(6.24m);
    }

    /// <summary>
    /// R6-F4(b): a buy stop-limit whose stop is above the adjusted High must not fill, even though
    /// raw High (29) reaches the stop and raw Close (28.82) is under the limit.
    /// </summary>
    [Fact]
    public async Task BuyStopLimit_StopAboveAdjustedHigh_DoesNotFill_WhenAdjustedCloseDiffersFromClose()
    {
        var fill = await FillOnAdjustedBarAsync(TradeAction.Buy, OrderType.StopLimit, primaryPrice: 10m, secondaryPrice: 30m).ConfigureAwait(false);

        fill.Should().BeNull(because: "the stop (10) is above the adjusted High (≈6.26)");
    }

    /// <summary>
    /// R6-F4(b): a buy stop-limit triggered within the adjusted range fills when the adjusted
    /// close (6.22) is at or under the limit; raw Close (28.82) must not block it.
    /// </summary>
    [Fact]
    public async Task BuyStopLimit_WithinAdjustedRange_Fills_WhenAdjustedCloseDiffersFromClose()
    {
        var fill = await FillOnAdjustedBarAsync(TradeAction.Buy, OrderType.StopLimit, primaryPrice: 6.24m, secondaryPrice: 6.25m).ConfigureAwait(false);

        fill.Should().NotBeNull(because: "adjusted High (≈6.26) >= stop (6.24) and adjusted Close (6.22) <= limit (6.25)");
        fill!.FillPrice.Should().Be(6.25m);
    }

    /// <summary>
    /// R6-F4(b): a sell stop-limit triggered within the adjusted range fills when the adjusted
    /// close (6.22) is at or above the limit; raw Low (28.6) must not block the trigger.
    /// </summary>
    [Fact]
    public async Task SellStopLimit_WithinAdjustedRange_Fills_WhenAdjustedCloseDiffersFromClose()
    {
        var fill = await FillOnAdjustedBarAsync(TradeAction.Sell, OrderType.StopLimit, primaryPrice: 6.20m, secondaryPrice: 6.21m).ConfigureAwait(false);

        fill.Should().NotBeNull(because: "adjusted Low (≈6.17) <= stop (6.20) and adjusted Close (6.22) >= limit (6.21)");
        fill!.FillPrice.Should().Be(6.21m);
    }

    /// <summary>
    /// R6-F4(b) guard: a bar with Close == 0 has no defined adjustment factor; the stop check
    /// falls back to the raw prices (factor 1), exactly as the market and limit paths do.
    /// </summary>
    [Fact]
    public async Task BuyStop_ZeroClose_FallsBackToRawHigh()
    {
        var today = new DateOnly(2024, 1, 15);
        var order = new Order(today, "Strategy1", new Symbol("AAPL"), TradeAction.Buy, OrderType.Stop, 10, PrimaryPrice: 105);
        var marketData = new SortedDictionary<Symbol, Bar>
        {
            { new Symbol("AAPL"), new Bar(today, Open: 100, High: 106, Low: 98, Close: 0, AdjustedClose: 0, Volume: 1000000) },
        };

        var eventTriggered = false;
        _simulatedBrokerage.FillOccurred += (_, _) => { eventTriggered = true; return Task.CompletedTask; };

        await _simulatedBrokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await _simulatedBrokerage.ProcessPendingOrdersAsync(today, marketData, CancellationToken.None).ConfigureAwait(false);

        eventTriggered.Should().BeTrue(because: "Close == 0 leaves the factor at 1, so raw High (106) >= stop (105)");
    }
}
