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
/// Tests for <see cref="SignalEventHandler"/> (R2I-13).
/// </summary>
public sealed class SignalEventHandlerTests
{
    private static readonly Asset s_testAsset = new("AAPL");
    private static readonly DateOnly s_testDate = new(2024, 1, 15);
    private const string StrategyName = "TestStrategy";

    private static (Mock<IPortfolio> Portfolio, Mock<IStrategy> Strategy, Mock<IPositionSizer> Sizer,
        Mock<IOrderPriceCalculationStrategy> PriceStrategy, Mock<IEventProcessor> EventProcessor) CreateMocks()
    {
        var portfolioMock = new Mock<IPortfolio>();
        var strategyMock = new Mock<IStrategy>();
        var sizerMock = new Mock<IPositionSizer>();
        var priceStrategyMock = new Mock<IOrderPriceCalculationStrategy>();
        var eventProcessorMock = new Mock<IEventProcessor>();

        strategyMock.Setup(s => s.PositionSizer).Returns(sizerMock.Object);
        strategyMock.Setup(s => s.OrderPriceCalculationStrategy).Returns(priceStrategyMock.Object);
        strategyMock.Setup(s => s.Positions).Returns(new Dictionary<Asset, int>());

        portfolioMock.Setup(p => p.GetStrategy(StrategyName)).Returns(strategyMock.Object);
        portfolioMock.Setup(p => p.HistoricalMarketData).Returns(
            new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>());
        portfolioMock.Setup(p => p.HistoricalFxConversionRates).Returns(
            new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>());
        portfolioMock.Setup(p => p.EventProcessor).Returns(eventProcessorMock.Object);

        return (portfolioMock, strategyMock, sizerMock, priceStrategyMock, eventProcessorMock);
    }

    [Fact]
    public async Task HandleEventAsync_BuySignal_GeneratesOrderEvent()
    {
        var (portfolio, _, sizer, priceStrategy, eventProcessor) = CreateMocks();

        sizer.Setup(s => s.ComputePositionSizes(
                It.IsAny<DateOnly>(), It.IsAny<IReadOnlyDictionary<Asset, SignalType>>(),
                It.IsAny<IStrategy>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>()))
            .Returns(new Dictionary<Asset, int> { [s_testAsset] = 100 });

        priceStrategy.Setup(p => p.CalculateOrderPrices(
                It.IsAny<DateOnly>(), It.IsAny<Asset>(), It.IsAny<TradeAction>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>()))
            .Returns((OrderType.Market, 185.50m, (decimal?)null));

        var signalEvent = new SignalEvent(s_testDate, StrategyName,
            new SortedDictionary<Asset, SignalType> { [s_testAsset] = SignalType.Overweight });

        var handler = new SignalEventHandler();
        await handler.HandleEventAsync(portfolio.Object, signalEvent, CancellationToken.None);

        eventProcessor.Verify(ep => ep.ProcessEventAsync(
            It.Is<OrderEvent>(o =>
                o.Asset == s_testAsset &&
                o.TradeAction == TradeAction.Buy &&
                o.Quantity == 100),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_SellSignal_GeneratesOrderWithSellAction()
    {
        var (portfolio, strategy, sizer, priceStrategy, eventProcessor) = CreateMocks();

        // Current position is 100, desired is 0 → sell 100
        strategy.Setup(s => s.Positions).Returns(new Dictionary<Asset, int> { [s_testAsset] = 100 });
        sizer.Setup(s => s.ComputePositionSizes(
                It.IsAny<DateOnly>(), It.IsAny<IReadOnlyDictionary<Asset, SignalType>>(),
                It.IsAny<IStrategy>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>()))
            .Returns(new Dictionary<Asset, int> { [s_testAsset] = 0 });

        priceStrategy.Setup(p => p.CalculateOrderPrices(
                It.IsAny<DateOnly>(), It.IsAny<Asset>(), It.IsAny<TradeAction>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>()))
            .Returns((OrderType.Market, 185.50m, (decimal?)null));

        var signalEvent = new SignalEvent(s_testDate, StrategyName,
            new SortedDictionary<Asset, SignalType> { [s_testAsset] = SignalType.Exit });

        var handler = new SignalEventHandler();
        await handler.HandleEventAsync(portfolio.Object, signalEvent, CancellationToken.None);

        eventProcessor.Verify(ep => ep.ProcessEventAsync(
            It.Is<OrderEvent>(o =>
                o.TradeAction == TradeAction.Sell &&
                o.Quantity == 100), // Math.Abs(-100)
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_ZeroOrderSize_SkipsOrder()
    {
        var (portfolio, strategy, sizer, _, eventProcessor) = CreateMocks();

        // Current position equals desired → no order
        strategy.Setup(s => s.Positions).Returns(new Dictionary<Asset, int> { [s_testAsset] = 100 });
        sizer.Setup(s => s.ComputePositionSizes(
                It.IsAny<DateOnly>(), It.IsAny<IReadOnlyDictionary<Asset, SignalType>>(),
                It.IsAny<IStrategy>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>()))
            .Returns(new Dictionary<Asset, int> { [s_testAsset] = 100 });

        var signalEvent = new SignalEvent(s_testDate, StrategyName,
            new SortedDictionary<Asset, SignalType> { [s_testAsset] = SignalType.Hold });

        var handler = new SignalEventHandler();
        await handler.HandleEventAsync(portfolio.Object, signalEvent, CancellationToken.None);

        eventProcessor.Verify(ep => ep.ProcessEventAsync(
            It.IsAny<IFinancialEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleEventAsync_NullPortfolio_ThrowsArgumentNullException()
    {
        var handler = new SignalEventHandler();
        var signalEvent = new SignalEvent(s_testDate, StrategyName,
            new SortedDictionary<Asset, SignalType> { [s_testAsset] = SignalType.Overweight });

        var act = () => handler.HandleEventAsync(null!, signalEvent, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleEventAsync_WrongEventType_ThrowsArgumentException()
    {
        var portfolioMock = new Mock<IPortfolio>();
        var handler = new SignalEventHandler();
        var marketEvent = new MarketEvent(s_testDate,
            new SortedDictionary<Asset, MarketData>(),
            new SortedDictionary<CurrencyCode, decimal>());

        var act = () => handler.HandleEventAsync(portfolioMock.Object, marketEvent, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*SignalEvent*");
    }

    [Fact]
    public async Task HandleEventAsync_CancellationToken_ThrowsOperationCanceled()
    {
        var portfolioMock = new Mock<IPortfolio>();
        var handler = new SignalEventHandler();
        var signalEvent = new SignalEvent(s_testDate, StrategyName,
            new SortedDictionary<Asset, SignalType> { [s_testAsset] = SignalType.Overweight });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => handler.HandleEventAsync(portfolioMock.Object, signalEvent, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
