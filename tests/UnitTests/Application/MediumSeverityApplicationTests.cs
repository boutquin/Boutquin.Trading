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
/// TDD tests verifying medium-severity application-layer fixes.
/// </summary>
public sealed class MediumSeverityApplicationTests
{
    private static readonly DateOnly s_testDate = new(2024, 1, 15);

    // ── BUG-A05: Limit buy order checks Low price, not Close ──────────
    [Fact]
    public async Task SimulatedBrokerage_LimitBuyOrder_ChecksLowPrice()
    {
        // Arrange — Low=50, Close=150. Limit buy at 60 should fill (Low <= 60).
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var marketData = CreateMarketData(open: 100, high: 200, low: 50, close: 150);
        SetupFetcher(fetcherMock, marketData);

        var brokerage = new SimulatedBrokerage(fetcherMock.Object);
        var order = CreateOrder(TradeAction.Buy, OrderType.Limit, primaryPrice: 60);

        FillEvent? capturedFill = null;
        brokerage.FillOccurred += (_, fill) =>
        {
            capturedFill = fill;
            return Task.CompletedTask;
        };

        // Act — SubmitOrderAsync queues; ProcessPendingOrdersAsync fills
        var queued = await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await brokerage.ProcessPendingOrdersAsync(s_testDate, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert — queued successfully and fills because Low (50) <= limit (60)
        queued.Should().BeTrue();
        capturedFill.Should().NotBeNull();
    }

    [Fact]
    public async Task SimulatedBrokerage_LimitBuyOrder_RejectsWhenLowAboveLimit()
    {
        // Arrange — Low=50. Limit buy at 40 should NOT fill (Low 50 > limit 40).
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var marketData = CreateMarketData(open: 100, high: 200, low: 50, close: 150);
        SetupFetcher(fetcherMock, marketData);

        var brokerage = new SimulatedBrokerage(fetcherMock.Object);
        var order = CreateOrder(TradeAction.Buy, OrderType.Limit, primaryPrice: 40);

        FillEvent? capturedFill = null;
        brokerage.FillOccurred += (_, fill) =>
        {
            capturedFill = fill;
            return Task.CompletedTask;
        };

        // Act — SubmitOrderAsync queues; ProcessPendingOrdersAsync evaluates
        var queued = await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await brokerage.ProcessPendingOrdersAsync(s_testDate, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert — queued successfully but no fill because Low (50) > limit (40)
        queued.Should().BeTrue();
        capturedFill.Should().BeNull();
    }

    [Fact]
    public async Task SimulatedBrokerage_LimitSellOrder_ChecksHighPrice()
    {
        // Arrange — High=200. Limit sell at 180 should fill (High 200 >= limit 180).
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var marketData = CreateMarketData(open: 100, high: 200, low: 50, close: 150);
        SetupFetcher(fetcherMock, marketData);

        var brokerage = new SimulatedBrokerage(fetcherMock.Object);
        var order = CreateOrder(TradeAction.Sell, OrderType.Limit, primaryPrice: 180);

        FillEvent? capturedFill = null;
        brokerage.FillOccurred += (_, fill) =>
        {
            capturedFill = fill;
            return Task.CompletedTask;
        };

        // Act — SubmitOrderAsync queues; ProcessPendingOrdersAsync fills
        var queued = await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await brokerage.ProcessPendingOrdersAsync(s_testDate, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert — queued successfully and fills because High (200) >= limit (180)
        queued.Should().BeTrue();
        capturedFill.Should().NotBeNull();
    }

    // ── BUG-A06: Zero split coefficient is guarded ────────────────────
    [Fact]
    public async Task MarketEventHandler_ZeroSplitCoefficient_Guarded()
    {
        // Arrange — splitCoefficient=0 should be skipped, not cause division by zero.
        var handler = new MarketEventHandler();
        var portfolioMock = new Mock<IPortfolio>();
        portfolioMock.Setup(p => p.IsLive).Returns(false);

        var asset = new Asset("AAPL");
        var marketData = new MarketData(
            Timestamp: s_testDate, Open: 100, High: 110, Low: 90, Close: 105,
            AdjustedClose: 105, Volume: 1_000_000,
            DividendPerShare: 0, SplitCoefficient: 0);

        var marketEvent = new MarketEvent(
            s_testDate,
            new SortedDictionary<Asset, MarketData> { { asset, marketData } },
            new SortedDictionary<CurrencyCode, decimal>());

        portfolioMock.Setup(p => p.GenerateSignals(marketEvent))
            .Returns([]);

        // Act — should not throw
        var act = async () => await handler.HandleEventAsync(portfolioMock.Object, marketEvent, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync();
        // AdjustPositionForSplit should NOT have been called (splitCoefficient is 0)
        portfolioMock.Verify(p => p.AdjustPositionForSplit(It.IsAny<Asset>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task MarketEventHandler_SplitCoefficientOne_Skipped()
    {
        // Arrange — splitCoefficient=1 means no split; should be skipped.
        var handler = new MarketEventHandler();
        var portfolioMock = new Mock<IPortfolio>();
        portfolioMock.Setup(p => p.IsLive).Returns(false);

        var asset = new Asset("AAPL");
        var marketData = new MarketData(
            Timestamp: s_testDate, Open: 100, High: 110, Low: 90, Close: 105,
            AdjustedClose: 105, Volume: 1_000_000,
            DividendPerShare: 0, SplitCoefficient: 1);

        var marketEvent = new MarketEvent(
            s_testDate,
            new SortedDictionary<Asset, MarketData> { { asset, marketData } },
            new SortedDictionary<CurrencyCode, decimal>());

        portfolioMock.Setup(p => p.GenerateSignals(marketEvent))
            .Returns([]);

        // Act
        await handler.HandleEventAsync(portfolioMock.Object, marketEvent, CancellationToken.None).ConfigureAwait(false);

        // Assert
        portfolioMock.Verify(p => p.AdjustPositionForSplit(It.IsAny<Asset>(), It.IsAny<decimal>()), Times.Never);
    }

    // ── BUG-A08: Empty market data inner dict throws meaningful error ──
    [Fact]
    public void FixedWeightPositionSizer_EmptyMarketData_Throws()
    {
        // Arrange
        var asset = new Asset("AAPL");
        var weights = new Dictionary<Asset, decimal> { { asset, 0.5m } };
        var sizer = new FixedWeightPositionSizer(weights, CurrencyCode.USD);

        var strategy = new TestStrategy
        {
            Name = "Test",
            Assets = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100_000m } },
        };

        // historicalMarketData has the date but empty inner dictionary (asset not found)
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { s_testDate, new SortedDictionary<Asset, MarketData>() }
        };
        var historicalFx = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { s_testDate, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var act = () => sizer.ComputePositionSizes(
            s_testDate,
            new Dictionary<Asset, SignalType> { { asset, SignalType.Overweight } },
            strategy,
            historicalMarketData,
            historicalFx);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Market data not found*");
    }

    // ── BUG-A09: AdjustedClose == 0 throws ───────────────────────────
    [Fact]
    public void FixedWeightPositionSizer_ZeroPrice_Throws()
    {
        // Arrange
        var asset = new Asset("AAPL");
        var weights = new Dictionary<Asset, decimal> { { asset, 0.5m } };
        var sizer = new FixedWeightPositionSizer(weights, CurrencyCode.USD);

        var strategy = new TestStrategy
        {
            Name = "Test",
            Assets = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100_000m } },
        };

        var zeroCloseData = new MarketData(
            Timestamp: s_testDate, Open: 100, High: 110, Low: 90, Close: 0,
            AdjustedClose: 0, Volume: 1_000_000,
            DividendPerShare: 0, SplitCoefficient: 1);

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { s_testDate, new SortedDictionary<Asset, MarketData> { { asset, zeroCloseData } } }
        };
        var historicalFx = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { s_testDate, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var act = () => sizer.ComputePositionSizes(
            s_testDate,
            new Dictionary<Asset, SignalType> { { asset, SignalType.Overweight } },
            strategy,
            historicalMarketData,
            historicalFx);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AdjustedClose is zero*");
    }

    // ── ROB-A03: Negative weights rejected ────────────────────────────
    [Fact]
    public void FixedWeightPositionSizer_NegativeWeights_Throws()
    {
        // Arrange
        var asset = new Asset("AAPL");
        var weights = new Dictionary<Asset, decimal> { { asset, -0.5m } };

        // Act
        var act = () => new FixedWeightPositionSizer(weights, CurrencyCode.USD);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FixedWeightPositionSizer_ZeroWeight_Allowed()
    {
        // Arrange — zero weight is valid (no position)
        var asset = new Asset("AAPL");
        var weights = new Dictionary<Asset, decimal> { { asset, 0m } };

        // Act
        var act = () => new FixedWeightPositionSizer(weights, CurrencyCode.USD);

        // Assert
        act.Should().NotThrow();
    }

    // ── ROB-A01: Custom commission rate applied ───────────────────────
    [Fact]
    public async Task SimulatedBrokerage_CustomCommission_Applied()
    {
        // Arrange — custom commission of 0.5%
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var marketData = CreateMarketData(open: 100, high: 200, low: 50, close: 150);
        SetupFetcher(fetcherMock, marketData);

        var customRate = 0.005m;
        var brokerage = new SimulatedBrokerage(fetcherMock.Object, customRate);

        FillEvent? capturedFill = null;
        brokerage.FillOccurred += (_, fill) =>
        {
            capturedFill = fill;
            return Task.CompletedTask;
        };

        var order = CreateOrder(TradeAction.Buy, OrderType.Market, quantity: 100);

        // Act — SubmitOrderAsync queues; ProcessPendingOrdersAsync fills at Open price
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await brokerage.ProcessPendingOrdersAsync(s_testDate, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert — commission = fillPrice * quantity * rate = 100 (Open) * 100 * 0.005 = 50
        capturedFill.Should().NotBeNull();
        capturedFill!.Commission.Should().Be(50m);
    }

    [Fact]
    public async Task SimulatedBrokerage_DefaultCommission_Is001()
    {
        // Arrange — default commission of 0.1%
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var marketData = CreateMarketData(open: 100, high: 200, low: 50, close: 150);
        SetupFetcher(fetcherMock, marketData);

        var brokerage = new SimulatedBrokerage(fetcherMock.Object);

        FillEvent? capturedFill = null;
        brokerage.FillOccurred += (_, fill) =>
        {
            capturedFill = fill;
            return Task.CompletedTask;
        };

        var order = CreateOrder(TradeAction.Buy, OrderType.Market, quantity: 100);

        // Act — SubmitOrderAsync queues; ProcessPendingOrdersAsync fills at Open price
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(false);
        await brokerage.ProcessPendingOrdersAsync(s_testDate, marketData, CancellationToken.None).ConfigureAwait(false);

        // Assert — commission = 100 (Open) * 100 * 0.001 = 10
        capturedFill.Should().NotBeNull();
        capturedFill!.Commission.Should().Be(10m);
    }

    // ── BUG-A07: Backtest filters market data to startDate..endDate ──
    [Fact]
    public async Task Backtest_RunAsync_RespectsDateRange()
    {
        // Arrange — 3 days of data, but backtest window is only day 2.
        var day1 = new DateOnly(2024, 1, 14);
        var day2 = new DateOnly(2024, 1, 15);
        var day3 = new DateOnly(2024, 1, 16);

        var asset = new Asset("AAPL");
        var strategy = new TestStrategy
        {
            Name = "Test",
            Assets = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } },
        };

        // AnalyzePerformanceMetrics needs sufficient equity points for all statistics.
        // Provide 5 points with varied returns (some negative) for Sortino/DownsideDeviation.
        var eq = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2024, 1, 10), 10000m },
            { new DateOnly(2024, 1, 11), 10100m },
            { new DateOnly(2024, 1, 12), 10050m },
            { day2, 10080m },
            { day3, 10120m },
        };

        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { { "Test", strategy } });
        portfolio.Setup(p => p.EquityCurve).Returns(eq);

        var benchEq = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2024, 1, 10), 10000m },
            { new DateOnly(2024, 1, 11), 10050m },
            { new DateOnly(2024, 1, 12), 10080m },
            { day2, 10060m },
            { day3, 10090m },
        };

        var benchmark = new Mock<IPortfolio>();
        benchmark.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());
        benchmark.Setup(p => p.EquityCurve).Returns(benchEq);

        var md = new MarketData(day2, 100, 110, 90, 105, 105, 1_000_000, 0, 1);
        var fetcher = new Mock<IMarketDataFetcher>();
        // Return 3 days of data
        fetcher.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[]
            {
                new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(day1, new SortedDictionary<Asset, MarketData> { { asset, md } }),
                new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(day2, new SortedDictionary<Asset, MarketData> { { asset, md } }),
                new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(day3, new SortedDictionary<Asset, MarketData> { { asset, md } }),
            }.ToAsyncEnumerable());
        fetcher.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>());

        var backtest = new BackTest(portfolio.Object, benchmark.Object, fetcher.Object, CurrencyCode.USD);

        // Act — window is day2..day2 only
        await backtest.RunAsync(day2, day3).ConfigureAwait(false);

        // Assert — HandleEventAsync should be called for day2 and day3, but NOT day1
        portfolio.Verify(p => p.HandleEventAsync(It.Is<MarketEvent>(e => e.Timestamp == day1), It.IsAny<CancellationToken>()), Times.Never);
        portfolio.Verify(p => p.HandleEventAsync(It.Is<MarketEvent>(e => e.Timestamp == day2), It.IsAny<CancellationToken>()), Times.Once);
        portfolio.Verify(p => p.HandleEventAsync(It.Is<MarketEvent>(e => e.Timestamp == day3), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ERR-A03: SignalEventHandler throws on missing position size ────
    [Fact]
    public async Task SignalEventHandler_MissingPositionSize_Throws()
    {
        // Arrange — strategy has asset AAPL but position sizer returns empty dict (missing AAPL)
        var handler = new SignalEventHandler();
        var asset = new Asset("AAPL");

        var positionSizerMock = new Mock<IPositionSizer>();
        positionSizerMock.Setup(ps => ps.ComputePositionSizes(
                It.IsAny<DateOnly>(), It.IsAny<IReadOnlyDictionary<Asset, SignalType>>(),
                It.IsAny<IStrategy>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>()))
            .Returns(new Dictionary<Asset, int>()); // Empty — no position for AAPL

        var orderPriceCalcMock = new Mock<IOrderPriceCalculationStrategy>();

        var strategy = new TestStrategy
        {
            Name = "Test",
            PositionSizer = positionSizerMock.Object,
            OrderPriceCalculationStrategy = orderPriceCalcMock.Object,
            Assets = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } },
        };

        var portfolioMock = new Mock<IPortfolio>();
        portfolioMock.Setup(p => p.GetStrategy("Test")).Returns(strategy);
        portfolioMock.Setup(p => p.HistoricalMarketData)
            .Returns(new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>());
        portfolioMock.Setup(p => p.HistoricalFxConversionRates)
            .Returns(new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>());

        var signalEvent = new SignalEvent(s_testDate, "Test",
            new Dictionary<Asset, SignalType> { { asset, SignalType.Overweight } });

        // Act
        var act = async () => await handler.HandleEventAsync(portfolioMock.Object, signalEvent, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Position size not computed*");
    }

    // ── M15: OrderEventHandler now logs warning instead of throwing on failed order ──
    [Fact]
    public async Task OrderEventHandler_FailedOrder_ShouldNotThrow()
    {
        // Arrange
        var handler = new OrderEventHandler();
        var portfolioMock = new Mock<IPortfolio>();
        var orderEvent = new OrderEvent(s_testDate, "Test", new Asset("AAPL"),
            TradeAction.Buy, OrderType.Market, 10, null, null);

        portfolioMock.Setup(p => p.SubmitOrderAsync(orderEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Order fails

        // Act
        var act = async () => await handler.HandleEventAsync(portfolioMock.Object, orderEvent, CancellationToken.None).ConfigureAwait(false);

        // Assert — M15: now logs instead of throwing
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OrderEventHandler_SuccessfulOrder_DoesNotThrow()
    {
        // Arrange
        var handler = new OrderEventHandler();
        var portfolioMock = new Mock<IPortfolio>();
        var orderEvent = new OrderEvent(s_testDate, "Test", new Asset("AAPL"),
            TradeAction.Buy, OrderType.Market, 10, null, null);

        portfolioMock.Setup(p => p.SubmitOrderAsync(orderEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = async () => await handler.HandleEventAsync(portfolioMock.Object, orderEvent, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── ROB-A02: FixedWeightPositionSizer throws on missing weight ────
    [Fact]
    public void FixedWeightPositionSizer_MissingWeight_Throws()
    {
        // Arrange — weights has AAPL but strategy has MSFT too
        var aapl = new Asset("AAPL");
        var msft = new Asset("MSFT");
        var weights = new Dictionary<Asset, decimal> { { aapl, 0.5m } }; // No MSFT weight
        var sizer = new FixedWeightPositionSizer(weights, CurrencyCode.USD);

        var strategy = new TestStrategy
        {
            Name = "Test",
            Assets = new Dictionary<Asset, CurrencyCode>
            {
                { aapl, CurrencyCode.USD },
                { msft, CurrencyCode.USD },
            },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100_000m } },
        };

        var md = new MarketData(s_testDate, 100, 110, 90, 105, 105, 1_000_000, 0, 1);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { s_testDate, new SortedDictionary<Asset, MarketData> { { aapl, md }, { msft, md } } }
        };
        var historicalFx = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { s_testDate, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var act = () => sizer.ComputePositionSizes(
            s_testDate,
            new Dictionary<Asset, SignalType> { { aapl, SignalType.Overweight }, { msft, SignalType.Overweight } },
            strategy,
            historicalMarketData,
            historicalFx);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Fixed asset weight not found*");
    }

    // ── ROB-A04: Portfolio.GenerateSignals guards null MarketEvent ─────
    [Fact]
    public void Portfolio_GenerateSignals_NullMarketEvent_Throws()
    {
        // Arrange — real Portfolio to verify the Guard fires on null input.
        var brokerageMock = new Mock<IBrokerage>();
        var asset = new Asset("AAPL");

        var strategy = new TestStrategy
        {
            Name = "Test",
            Assets = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100_000m } },
        };

        var handlers = new Dictionary<Type, IEventHandler>
        {
            { typeof(MarketEvent), new MarketEventHandler() },
        };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new Dictionary<string, IStrategy> { { "Test", strategy } },
            new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } },
            handlers,
            brokerageMock.Object);

        // Act
        var act = () => portfolio.GenerateSignals(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static SortedDictionary<Asset, MarketData> CreateMarketData(
        decimal open, decimal high, decimal low, decimal close)
    {
        return new SortedDictionary<Asset, MarketData>
        {
            {
                new Asset("AAPL"),
                new MarketData(
                    Timestamp: s_testDate, Open: open, High: high, Low: low, Close: close,
                    AdjustedClose: close, Volume: 1_000_000,
                    DividendPerShare: 0, SplitCoefficient: 1)
            }
        };
    }

    private static void SetupFetcher(
        Mock<IMarketDataFetcher> mock,
        SortedDictionary<Asset, MarketData> marketData)
    {
        var kvp = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(s_testDate, marketData);
        mock.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { kvp }.ToAsyncEnumerable());
    }

    private static Order CreateOrder(
        TradeAction action, OrderType type,
        decimal? primaryPrice = null, int quantity = 10)
    {
        return new Order(
            Timestamp: s_testDate,
            StrategyName: "TestStrategy",
            Asset: new Asset("AAPL"),
            TradeAction: action,
            OrderType: type,
            Quantity: quantity,
            PrimaryPrice: primaryPrice);
    }
}
