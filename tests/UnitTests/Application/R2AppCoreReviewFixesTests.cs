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

using Boutquin.Trading.Application.PortfolioConstruction;
using FluentAssertions;

/// <summary>
/// Tests for round-2 application-core review fixes (R2C-01 through R2C-06).
/// </summary>
public sealed class R2AppCoreReviewFixesTests
{
    private static readonly DateOnly s_today = new(2024, 1, 15);
    private static readonly Asset s_aapl = new("AAPL");

    private static MarketData MakeMarketData(
        decimal open = 100m, decimal high = 200m, decimal low = 50m,
        decimal close = 150m, decimal adjustedClose = 150m, long volume = 1_000_000)
        => new(s_today, open, high, low, close, adjustedClose, volume, 0m, 1m);

    private static KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>> MakeMarketDataKvp(MarketData md)
        => new(s_today, new SortedDictionary<Asset, MarketData> { { s_aapl, md } });

    private static Mock<IMarketDataFetcher> SetupFetcher(MarketData md)
    {
        var mock = new Mock<IMarketDataFetcher>();
        mock.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { MakeMarketDataKvp(md) }.ToAsyncEnumerable());
        return mock;
    }

    #region R2C-02: FillEventHandler sell quantity negation

    /// <summary>
    /// R2C-02: Sell fill must call UpdatePositions with NEGATIVE quantity to reduce position.
    /// </summary>
    [Fact]
    public async Task HandleFillEvent_SellOrder_ReducesPosition()
    {
        // Arrange
        var strategyName = "TestStrategy";
        var mockStrategy = new Mock<IStrategy>();
        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.GetStrategy(strategyName)).Returns(mockStrategy.Object);
        mockPortfolio.Setup(p => p.GetAssetCurrency(s_aapl)).Returns(CurrencyCode.USD);

        var sellFill = new FillEvent(s_today, s_aapl, strategyName, TradeAction.Sell, 100m, 50, 10m);
        var handler = new FillEventHandler();

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, sellFill, CancellationToken.None).ConfigureAwait(true);

        // Assert — sell must pass -50, not +50
        mockStrategy.Verify(s => s.UpdatePositions(s_aapl, -50), Times.Once);
    }

    /// <summary>
    /// R2C-02: Buy fill must call UpdatePositions with POSITIVE quantity.
    /// </summary>
    [Fact]
    public async Task HandleFillEvent_BuyOrder_IncreasesPosition()
    {
        // Arrange
        var strategyName = "TestStrategy";
        var mockStrategy = new Mock<IStrategy>();
        mockStrategy.Setup(s => s.Cash).Returns(
            new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100000m } });
        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.GetStrategy(strategyName)).Returns(mockStrategy.Object);
        mockPortfolio.Setup(p => p.GetAssetCurrency(s_aapl)).Returns(CurrencyCode.USD);

        var buyFill = new FillEvent(s_today, s_aapl, strategyName, TradeAction.Buy, 100m, 50, 10m);
        var handler = new FillEventHandler();

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, buyFill, CancellationToken.None).ConfigureAwait(true);

        // Assert — buy must pass +50
        mockStrategy.Verify(s => s.UpdatePositions(s_aapl, 50), Times.Once);
    }

    /// <summary>
    /// R2C-02: Sell fill credits cash (tradeValue - commission).
    /// </summary>
    [Fact]
    public async Task HandleFillEvent_SellOrder_CreditsCash()
    {
        // Arrange
        var strategyName = "TestStrategy";
        var mockStrategy = new Mock<IStrategy>();
        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.GetStrategy(strategyName)).Returns(mockStrategy.Object);
        mockPortfolio.Setup(p => p.GetAssetCurrency(s_aapl)).Returns(CurrencyCode.USD);

        // 50 shares at $100, $10 commission → credit = 5000 - 10 = 4990
        var sellFill = new FillEvent(s_today, s_aapl, strategyName, TradeAction.Sell, 100m, 50, 10m);
        var handler = new FillEventHandler();

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, sellFill, CancellationToken.None).ConfigureAwait(true);

        // Assert
        mockStrategy.Verify(s => s.UpdateCash(CurrencyCode.USD, 4990m), Times.Once);
    }

    #endregion

    #region R2C-01: StopLimit High/Low trigger

    /// <summary>
    /// R2C-01: StopLimit buy should trigger on High (not Close) and fill when Close is at or below limit.
    /// </summary>
    [Fact]
    public async Task HandleStopLimitOrder_BuyTrigger_UsesHighNotClose()
    {
        // Arrange: stopPrice=105, limitPrice=107. Close=100, High=106, Low=99.
        // Stop triggers: High(106) >= 105. Limit fills: Close(100) <= 107.
        var md = MakeMarketData(open: 100, high: 106, low: 99, close: 100, adjustedClose: 100);
        var fetcherMock = SetupFetcher(md);
        var brokerage = new SimulatedBrokerage(fetcherMock.Object);

        var order = new Order(s_today, "S1", s_aapl, TradeAction.Buy, OrderType.StopLimit, 10,
            PrimaryPrice: 105m, SecondaryPrice: 107m);

        var filled = false;
        brokerage.FillOccurred += (_, _) => { filled = true; return Task.CompletedTask; };

        // Act — queue the order, then process pending orders with next-bar data
        var result = await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);
        var dayData = new SortedDictionary<Asset, MarketData> { { s_aapl, md } };
        await brokerage.ProcessPendingOrdersAsync(s_today, dayData, CancellationToken.None).ConfigureAwait(true);

        // Assert — stop triggers (High>=stop) and limit fills (Close<=limit)
        result.Should().BeTrue("SubmitOrderAsync always queues the order");
        filled.Should().BeTrue();
    }

    /// <summary>
    /// R2C-01: StopLimit sell should trigger on Low (not Close) and fill when Close >= limit.
    /// </summary>
    [Fact]
    public async Task HandleStopLimitOrder_SellTrigger_UsesLowNotClose()
    {
        // Arrange: stopPrice=95, limitPrice=93. Close=100, Low=94, High=101.
        // Stop triggers: Low(94) <= 95. Limit fills: Close(100) >= 93.
        var md = MakeMarketData(open: 100, high: 101, low: 94, close: 100, adjustedClose: 100);
        var fetcherMock = SetupFetcher(md);
        var brokerage = new SimulatedBrokerage(fetcherMock.Object);

        var order = new Order(s_today, "S1", s_aapl, TradeAction.Sell, OrderType.StopLimit, 10,
            PrimaryPrice: 95m, SecondaryPrice: 93m);

        var filled = false;
        brokerage.FillOccurred += (_, _) => { filled = true; return Task.CompletedTask; };

        // Act — queue the order, then process pending orders with next-bar data
        var result = await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);
        var dayData = new SortedDictionary<Asset, MarketData> { { s_aapl, md } };
        await brokerage.ProcessPendingOrdersAsync(s_today, dayData, CancellationToken.None).ConfigureAwait(true);

        // Assert
        result.Should().BeTrue("SubmitOrderAsync always queues the order");
        filled.Should().BeTrue();
    }

    /// <summary>
    /// R2C-01: StopLimit buy should NOT trigger when High is below stopPrice.
    /// </summary>
    [Fact]
    public async Task HandleStopLimitOrder_HighBelowStop_NoTrigger()
    {
        // Arrange: stopPrice=110, High=108 → stop doesn't trigger
        var md = MakeMarketData(open: 100, high: 108, low: 99, close: 105, adjustedClose: 105);
        var fetcherMock = SetupFetcher(md);
        var brokerage = new SimulatedBrokerage(fetcherMock.Object);

        var order = new Order(s_today, "S1", s_aapl, TradeAction.Buy, OrderType.StopLimit, 10,
            PrimaryPrice: 110m, SecondaryPrice: 115m);

        var filled = false;
        brokerage.FillOccurred += (_, _) => { filled = true; return Task.CompletedTask; };

        // Act — queue the order, then process; stop should not trigger so no fill
        var result = await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);
        var dayData = new SortedDictionary<Asset, MarketData> { { s_aapl, md } };
        await brokerage.ProcessPendingOrdersAsync(s_today, dayData, CancellationToken.None).ConfigureAwait(true);

        // Assert — order was queued (true) but no fill occurred
        result.Should().BeTrue("SubmitOrderAsync always queues the order");
        filled.Should().BeFalse();
    }

    /// <summary>
    /// R2C-01: StopLimit buy — stop triggers but limit missed (Close > limitPrice) → no fill.
    /// </summary>
    [Fact]
    public async Task HandleStopLimitOrder_StopTriggered_LimitMissed_NoFill()
    {
        // Arrange: stopPrice=105, limitPrice=99. High=106 (stop triggers), Close=100 (100>99, limit missed).
        var md = MakeMarketData(open: 100, high: 106, low: 98, close: 100, adjustedClose: 100);
        var fetcherMock = SetupFetcher(md);
        var brokerage = new SimulatedBrokerage(fetcherMock.Object);

        var order = new Order(s_today, "S1", s_aapl, TradeAction.Buy, OrderType.StopLimit, 10,
            PrimaryPrice: 105m, SecondaryPrice: 99m);

        var filled = false;
        brokerage.FillOccurred += (_, _) => { filled = true; return Task.CompletedTask; };

        // Act — queue the order, then process; stop triggers but limit missed so no fill
        var result = await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);
        var dayData = new SortedDictionary<Asset, MarketData> { { s_aapl, md } };
        await brokerage.ProcessPendingOrdersAsync(s_today, dayData, CancellationToken.None).ConfigureAwait(true);

        // Assert — order was queued (true) but no fill occurred (stop triggered, limit missed)
        result.Should().BeTrue("SubmitOrderAsync always queues the order");
        filled.Should().BeFalse();
    }

    #endregion

    #region R2C-04: Multicast delegate iteration

    /// <summary>
    /// R2C-04: Multiple FillOccurred handlers must ALL be awaited.
    /// </summary>
    [Fact]
    public async Task FillOccurred_MultipleHandlers_AllAwaited()
    {
        // Arrange
        var md = MakeMarketData();
        var fetcherMock = SetupFetcher(md);
        var brokerage = new SimulatedBrokerage(fetcherMock.Object);

        var order = new Order(s_today, "S1", s_aapl, TradeAction.Buy, OrderType.Market, 10);

        var handler1Called = false;
        var handler2Called = false;

        brokerage.FillOccurred += (_, _) => { handler1Called = true; return Task.CompletedTask; };
        brokerage.FillOccurred += (_, _) => { handler2Called = true; return Task.CompletedTask; };

        // Act — queue the order, then process pending orders to trigger fills
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);
        var dayData = new SortedDictionary<Asset, MarketData> { { s_aapl, md } };
        await brokerage.ProcessPendingOrdersAsync(s_today, dayData, CancellationToken.None).ConfigureAwait(true);

        // Assert — both handlers must have been called
        handler1Called.Should().BeTrue("first handler should be invoked");
        handler2Called.Should().BeTrue("second handler should be invoked");
    }

    /// <summary>
    /// R2C-04: Handler that throws should propagate exception (not swallowed).
    /// </summary>
    [Fact]
    public async Task FillOccurred_HandlerThrows_ExceptionPropagates()
    {
        // Arrange
        var md = MakeMarketData();
        var fetcherMock = SetupFetcher(md);
        var brokerage = new SimulatedBrokerage(fetcherMock.Object);

        var order = new Order(s_today, "S1", s_aapl, TradeAction.Buy, OrderType.Market, 10);

        brokerage.FillOccurred += (_, _) => throw new InvalidOperationException("test error");

        // Act — queue the order, then process; exception should propagate from ProcessPendingOrdersAsync
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);
        var dayData = new SortedDictionary<Asset, MarketData> { { s_aapl, md } };
        var act = () => brokerage.ProcessPendingOrdersAsync(s_today, dayData, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("test error").ConfigureAwait(true);
    }

    #endregion

    #region R2C-05: ComputeCurrentWeights FX rate throw

    /// <summary>
    /// R2C-05: When FX rate is missing for a multi-currency portfolio, GenerateSignals should
    /// NOT throw (M9 catch handles it) and should still produce rebalance signals.
    /// </summary>
    [Fact]
    public void GenerateSignals_MissingFxRate_FallsBackToEmptyWeights()
    {
        // Arrange: 2 assets — one USD, one EUR. FX rates missing EUR.
        var vti = new Asset("VTI");
        var dax = new Asset("DAX");
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            [vti] = CurrencyCode.USD,
            [dax] = CurrencyCode.EUR
        };
        var cash = new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 100_000m };

        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();

        var strategy = new ConstructionModelStrategy(
            "FX Test", assets, cash, orderPriceCalc, positionSizer,
            constructionModel, RebalancingFrequency.Daily, lookbackWindow: 5);

        // Build market data with both assets
        var marketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
        var baseDate = new DateOnly(2024, 1, 2);
        for (var i = 0; i < 10; i++)
        {
            var date = baseDate.AddDays(i);
            marketData[date] = new SortedDictionary<Asset, MarketData>
            {
                [vti] = new(date, 200m, 201m, 199m, 200m, 200m, 1_000_000, 0m),
                [dax] = new(date, 100m, 101m, 99m, 100m, 100m, 500_000, 0m)
            };
        }

        // FX rates: only USD, missing EUR
        var fxRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();
        for (var i = 0; i < 10; i++)
        {
            var date = baseDate.AddDays(i);
            fxRates[date] = new SortedDictionary<CurrencyCode, decimal>
            {
                [CurrencyCode.USD] = 1.0m
                // EUR intentionally missing
            };
        }

        // First call sets _lastRebalancingDate
        strategy.GenerateSignals(baseDate, CurrencyCode.USD, marketData, fxRates);

        // Act — second call hits ComputeCurrentWeights with missing EUR FX rate
        var nextDay = baseDate.AddDays(1);
        var act = () => strategy.GenerateSignals(nextDay, CurrencyCode.USD, marketData, fxRates);

        // Assert — M9 catch prevents throw; strategy proceeds with rebalance
        act.Should().NotThrow("M9 catch should handle InvalidOperationException from missing FX rate");
    }

    #endregion

    #region R2C-03: MarketEventHandler signal capture (verification — N/A)

    /// <summary>
    /// R2C-03 verification: MarketEventHandler captures and processes all signals from GenerateSignals.
    /// </summary>
    [Fact]
    public async Task HandleMarketEvent_CapturesAndProcessesSignals()
    {
        // Arrange
        var date = s_today;
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            { s_aapl, new MarketData(date, 100m, 105m, 99m, 104m, 104m, 1000, 0m, 1m) }
        };
        var fxRates = new SortedDictionary<CurrencyCode, decimal>();
        var marketEvent = new MarketEvent(date, marketData, fxRates);

        var signal1 = new SignalEvent(date, "S1", new Dictionary<Asset, SignalType>
        {
            { s_aapl, SignalType.Overweight }
        });
        var signal2 = new SignalEvent(date, "S2", new Dictionary<Asset, SignalType>
        {
            { s_aapl, SignalType.Underweight }
        });

        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.IsLive).Returns(false);
        mockPortfolio.Setup(p => p.GenerateSignals(marketEvent)).Returns([signal1, signal2]);

        var processedEvents = new List<IFinancialEvent>();
        mockPortfolio.Setup(p => p.HandleEventAsync(It.IsAny<IFinancialEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IFinancialEvent, CancellationToken>((e, _) => processedEvents.Add(e))
            .Returns(Task.CompletedTask);

        var handler = new MarketEventHandler();

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, marketEvent, CancellationToken.None).ConfigureAwait(true);

        // Assert — both signals should be processed
        processedEvents.Should().HaveCount(2);
        processedEvents.Should().Contain(signal1);
        processedEvents.Should().Contain(signal2);
    }

    /// <summary>
    /// R2C-03 verification: No signals → HandleEventAsync not called for signals.
    /// </summary>
    [Fact]
    public async Task HandleMarketEvent_NoSignals_NoProcessing()
    {
        // Arrange
        var date = s_today;
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            { s_aapl, new MarketData(date, 100m, 105m, 99m, 104m, 104m, 1000, 0m, 1m) }
        };
        var fxRates = new SortedDictionary<CurrencyCode, decimal>();
        var marketEvent = new MarketEvent(date, marketData, fxRates);

        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.IsLive).Returns(false);
        mockPortfolio.Setup(p => p.GenerateSignals(marketEvent)).Returns([]);

        var handler = new MarketEventHandler();

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, marketEvent, CancellationToken.None).ConfigureAwait(true);

        // Assert — HandleEventAsync never called for signals (only UpdateHistoricalData was called)
        mockPortfolio.Verify(
            p => p.HandleEventAsync(It.IsAny<IFinancialEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region R2C-06: SimulatedBrokerage timestamp filtering (verification — N/A)

    /// <summary>
    /// R2C-06 verification: Order processes with correct-date data when ProcessPendingOrdersAsync
    /// is called with that date's market data. Market orders fill at Open price.
    /// </summary>
    [Fact]
    public async Task SubmitOrder_ReturnsDataForOrderDate()
    {
        // Arrange: Order queued on date1, processed on date2 (next-bar fill).
        var date1 = new DateOnly(2024, 1, 14);
        var date2 = new DateOnly(2024, 1, 15);

        var md2 = new SortedDictionary<Asset, MarketData>
        {
            { s_aapl, new MarketData(date2, 100m, 110m, 95m, 105m, 105m, 1_000_000, 0m, 1m) }
        };

        var fetcherMock = new Mock<IMarketDataFetcher>();
        var brokerage = new SimulatedBrokerage(fetcherMock.Object);
        var order = new Order(date1, "S1", s_aapl, TradeAction.Buy, OrderType.Market, 10);

        FillEvent? capturedFill = null;
        brokerage.FillOccurred += (_, fill) => { capturedFill = fill; return Task.CompletedTask; };

        // Act — queue the order, then process with date2's data (next-bar fill)
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);
        await brokerage.ProcessPendingOrdersAsync(date2, md2, CancellationToken.None).ConfigureAwait(true);

        // Assert — market order fills at Open price (100), not Close (105)
        capturedFill.Should().NotBeNull();
        capturedFill!.FillPrice.Should().Be(100m, "market orders fill at Open price in next-bar fill model");
    }

    /// <summary>
    /// R2C-06 verification: When ProcessPendingOrdersAsync receives data without the order's asset,
    /// no fill occurs (order is silently dropped).
    /// </summary>
    [Fact]
    public async Task SubmitOrder_NoDataForOrderAsset_NoFill()
    {
        // Arrange: order for AAPL but day data contains a different asset
        var date1 = new DateOnly(2024, 1, 14);
        var date2 = new DateOnly(2024, 1, 15);

        var otherAsset = new Asset("MSFT");
        var dayData = new SortedDictionary<Asset, MarketData>
        {
            { otherAsset, new MarketData(date2, 100m, 110m, 95m, 105m, 105m, 1_000_000, 0m, 1m) }
        };

        var fetcherMock = new Mock<IMarketDataFetcher>();
        var brokerage = new SimulatedBrokerage(fetcherMock.Object);
        var order = new Order(date1, "S1", s_aapl, TradeAction.Buy, OrderType.Market, 10);

        var filled = false;
        brokerage.FillOccurred += (_, _) => { filled = true; return Task.CompletedTask; };

        // Act — queue the order, then process with data that doesn't contain AAPL
        var result = await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);
        await brokerage.ProcessPendingOrdersAsync(date2, dayData, CancellationToken.None).ConfigureAwait(true);

        // Assert — order was queued (true) but no fill because asset not in day data
        result.Should().BeTrue("SubmitOrderAsync always queues the order");
        filled.Should().BeFalse("no market data for AAPL in the day's data");
    }

    #endregion
}
