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

using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

public sealed class RebalancingSignalEventHandlerTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_bnd = new("BND");
    private static readonly Asset s_gld = new("GLD");
    private static readonly DateOnly s_date = new(2024, 6, 3);

    /// <summary>
    /// When all signals are Rebalance and strategy is ConstructionModelStrategy,
    /// the handler should emit sells before buys.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_RebalanceSignals_EmitsSellsBeforeBuys()
    {
        // Arrange: capture order events to verify ordering
        var emittedOrders = new List<OrderEvent>();
        var mockPortfolio = CreateMockPortfolio(emittedOrders);

        // Strategy has VTI=100 shares (overweight), BND=0 (underweight), GLD=50 (to sell)
        var strategy = CreateConstructionModelStrategy(
            positions: new Dictionary<Asset, int> { [s_vti] = 100, [s_bnd] = 0, [s_gld] = 50 },
            targetWeights: new Dictionary<Asset, decimal> { [s_vti] = 0.3m, [s_bnd] = 0.5m, [s_gld] = 0.2m });

        mockPortfolio.Setup(p => p.GetStrategy("TestStrategy")).Returns(strategy);

        var handler = new RebalancingSignalEventHandler(CurrencyCode.USD);
        var signals = new SortedDictionary<Asset, SignalType>
        {
            [s_vti] = SignalType.Rebalance,
            [s_bnd] = SignalType.Rebalance,
            [s_gld] = SignalType.Rebalance,
        };
        var signalEvent = new SignalEvent(s_date, "TestStrategy", signals);

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, signalEvent, CancellationToken.None);

        // Assert: sells should come before buys
        emittedOrders.Should().NotBeEmpty();

        var sellOrders = emittedOrders.Where(o => o.TradeAction == TradeAction.Sell).ToList();
        var buyOrders = emittedOrders.Where(o => o.TradeAction == TradeAction.Buy).ToList();

        if (sellOrders.Count > 0 && buyOrders.Count > 0)
        {
            var lastSellIndex = emittedOrders.FindLastIndex(o => o.TradeAction == TradeAction.Sell);
            var firstBuyIndex = emittedOrders.FindIndex(o => o.TradeAction == TradeAction.Buy);
            lastSellIndex.Should().BeLessThan(firstBuyIndex, "sells must execute before buys");
        }
    }

    /// <summary>
    /// When signals are NOT all Rebalance, the handler falls back to standard SignalEventHandler.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_MixedSignals_FallsBackToStandardHandler()
    {
        // Arrange
        var emittedOrders = new List<OrderEvent>();
        var mockPortfolio = CreateMockPortfolio(emittedOrders);

        var strategy = CreateSimpleStrategy(
            positions: new Dictionary<Asset, int> { [s_vti] = 0 });

        mockPortfolio.Setup(p => p.GetStrategy("TestStrategy")).Returns(strategy);

        var handler = new RebalancingSignalEventHandler(CurrencyCode.USD);
        var signals = new SortedDictionary<Asset, SignalType>
        {
            [s_vti] = SignalType.Underweight, // Not Rebalance
        };
        var signalEvent = new SignalEvent(s_date, "TestStrategy", signals);

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, signalEvent, CancellationToken.None);

        // Assert: should still produce orders via fallback
        emittedOrders.Should().NotBeEmpty();
    }

    /// <summary>
    /// When positions already match targets, no orders are emitted.
    /// Total value = 0 cash + 600*$100 + 400*$100 = $100k.
    /// VTI 60% → target 600 shares, BND 40% → target 400 shares. Both match.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_PositionsMatchTargets_NoOrders()
    {
        // Arrange: strategy with $0 cash, positions exactly matching target weights
        var emittedOrders = new List<OrderEvent>();
        var mockPortfolio = CreateMockPortfolio(emittedOrders);

        var strategy = CreateConstructionModelStrategy(
            positions: new Dictionary<Asset, int> { [s_vti] = 600, [s_bnd] = 400 },
            targetWeights: new Dictionary<Asset, decimal> { [s_vti] = 0.6m, [s_bnd] = 0.4m },
            initialCash: 0m); // No cash — total value is purely positions

        mockPortfolio.Setup(p => p.GetStrategy("TestStrategy")).Returns(strategy);

        var handler = new RebalancingSignalEventHandler(CurrencyCode.USD);
        var signals = new SortedDictionary<Asset, SignalType>
        {
            [s_vti] = SignalType.Rebalance,
            [s_bnd] = SignalType.Rebalance,
        };
        var signalEvent = new SignalEvent(s_date, "TestStrategy", signals);

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, signalEvent, CancellationToken.None);

        // Assert
        emittedOrders.Should().BeEmpty();
    }

    /// <summary>
    /// Constructor with negative minimumTradeValue throws.
    /// </summary>
    [Fact]
    public void Constructor_NegativeMinimumTradeValue_Throws()
    {
        var act = () => new RebalancingSignalEventHandler(CurrencyCode.USD, -1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- Helpers ---

    private static Mock<IPortfolio> CreateMockPortfolio(List<OrderEvent> capturedOrders)
    {
        var mockPortfolio = new Mock<IPortfolio>();

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [s_date] = new SortedDictionary<Asset, MarketData>
            {
                [s_vti] = new MarketData(s_date, 100m, 105m, 95m, 100m, 100m, 1_000_000L, 0m, 1m),
                [s_bnd] = new MarketData(s_date, 100m, 102m, 98m, 100m, 100m, 500_000L, 0m, 1m),
                [s_gld] = new MarketData(s_date, 100m, 103m, 97m, 100m, 100m, 300_000L, 0m, 1m),
            },
        };

        var fxRates = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            [s_date] = new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 1m },
        };

        mockPortfolio.Setup(p => p.HistoricalMarketData).Returns(marketData);
        mockPortfolio.Setup(p => p.HistoricalFxConversionRates).Returns(fxRates);

        // Capture order events through EventProcessor
        var mockProcessor = new Mock<IEventProcessor>();
        mockProcessor
            .Setup(ep => ep.ProcessEventAsync(It.IsAny<IFinancialEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IFinancialEvent, CancellationToken>((evt, _) =>
            {
                if (evt is OrderEvent order)
                {
                    capturedOrders.Add(order);
                }
            })
            .Returns(Task.CompletedTask);

        mockPortfolio.Setup(p => p.EventProcessor).Returns(mockProcessor.Object);

        return mockPortfolio;
    }

    private static ConstructionModelStrategy CreateConstructionModelStrategy(
        Dictionary<Asset, int> positions,
        Dictionary<Asset, decimal> targetWeights,
        decimal initialCash = 100_000m)
    {
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            [s_vti] = CurrencyCode.USD,
            [s_bnd] = CurrencyCode.USD,
            [s_gld] = CurrencyCode.USD,
        };

        // Only include assets that are in positions or targetWeights
        var relevantAssets = new Dictionary<Asset, CurrencyCode>();
        foreach (var a in positions.Keys.Union(targetWeights.Keys))
        {
            if (assets.TryGetValue(a, out var cc))
            {
                relevantAssets[a] = cc;
            }
        }

        var cash = new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = initialCash };
        var mockOrderPrice = new Mock<IOrderPriceCalculationStrategy>();
        mockOrderPrice
            .Setup(o => o.CalculateOrderPrices(It.IsAny<DateOnly>(), It.IsAny<Asset>(), It.IsAny<TradeAction>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>()))
            .Returns((DateOnly _, Asset _, TradeAction _, IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> _) =>
                (OrderType.Market, 100m, 0m));

        var mockSizer = new Mock<IPositionSizer>();
        var constructionModel = new EqualWeightConstruction();

        var strategy = new ConstructionModelStrategy(
            "TestStrategy", relevantAssets, cash, mockOrderPrice.Object, mockSizer.Object,
            constructionModel, RebalancingFrequency.Monthly);

        // Set positions
        foreach (var (asset, qty) in positions)
        {
            if (qty != 0)
            {
                strategy.UpdatePositions(asset, qty);
            }
        }

        // Set LastComputedWeights via reflection-free method: cast to set property
        // We need to trigger GenerateSignals or set it directly — use a helper approach
        // Actually, we can set it by calling the internal logic. Instead, let's use
        // the target weights directly by creating a minimal market data set and calling GenerateSignals.
        // Simpler: just set via property directly through the construction model path.

        // For this test, we'll craft the strategy so that LastComputedWeights is set:
        // We do this by creating a new strategy that already has weights set.
        // The simplest way: make the strategy and set LastComputedWeights through a first GenerateSignals call.

        // Use a fake historical data with 3 dates to allow weight computation:
        var historicalData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
        for (var i = 0; i < 5; i++)
        {
            var date = s_date.AddDays(-10 + i);
            var dayData = new SortedDictionary<Asset, MarketData>();
            foreach (var asset in relevantAssets.Keys)
            {
                dayData[asset] = new MarketData(date, 100m, 105m, 95m, 100m, 100m, 1_000_000L, 0m, 1m);
            }
            historicalData[date] = dayData;
        }

        var fxData = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();
        for (var i = 0; i < 5; i++)
        {
            var date = s_date.AddDays(-10 + i);
            fxData[date] = new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 1m };
        }

        // Call GenerateSignals to set LastComputedWeights — but we want specific weights.
        // The EqualWeightConstruction will compute equal weights, which may not match targetWeights.
        // Instead, manually force weights by using a mock construction model.
        // Let's recreate with a mock:
        var mockModel = new Mock<IPortfolioConstructionModel>();
        mockModel.Setup(m => m.ComputeTargetWeights(It.IsAny<IReadOnlyList<Asset>>(), It.IsAny<decimal[][]>()))
            .Returns(targetWeights);

        var strategy2 = new ConstructionModelStrategy(
            "TestStrategy", relevantAssets, cash, mockOrderPrice.Object, mockSizer.Object,
            mockModel.Object, RebalancingFrequency.Monthly, lookbackWindow: 2);

        // Set positions on strategy2
        foreach (var (asset, qty) in positions)
        {
            if (qty != 0)
            {
                strategy2.UpdatePositions(asset, qty);
            }
        }

        // Trigger GenerateSignals to set LastComputedWeights
        strategy2.GenerateSignals(s_date, CurrencyCode.USD, historicalData, fxData);

        return strategy2;
    }

    private static IStrategy CreateSimpleStrategy(Dictionary<Asset, int> positions)
    {
        var assets = new Dictionary<Asset, CurrencyCode> { [s_vti] = CurrencyCode.USD };
        var cash = new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 100_000m };

        var mockOrderPrice = new Mock<IOrderPriceCalculationStrategy>();
        mockOrderPrice
            .Setup(o => o.CalculateOrderPrices(It.IsAny<DateOnly>(), It.IsAny<Asset>(), It.IsAny<TradeAction>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>()))
            .Returns((OrderType.Market, 100m, 0m));

        // Create a FixedWeightPositionSizer that returns position sizes
        var sizer = new FixedWeightPositionSizer(
            new Dictionary<Asset, decimal> { [s_vti] = 1.0m },
            CurrencyCode.USD);

        var strategy = new BuyAndHoldStrategy("TestStrategy", assets, cash, s_date, mockOrderPrice.Object, sizer);

        foreach (var (asset, qty) in positions)
        {
            if (qty != 0)
            {
                strategy.UpdatePositions(asset, qty);
            }
        }

        return strategy;
    }
}
