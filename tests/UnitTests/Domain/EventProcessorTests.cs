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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// Tests for <see cref="EventProcessor"/> (R2I-12).
/// </summary>
public sealed class EventProcessorTests
{
    private static readonly Asset s_testAsset = new("AAPL");
    private static readonly DateOnly s_testDate = new(2024, 1, 15);

    private static MarketEvent CreateMarketEvent() =>
        new(s_testDate,
            new SortedDictionary<Asset, MarketData>(),
            new SortedDictionary<CurrencyCode, decimal>());

    [Fact]
    public async Task ProcessEventAsync_MarketEvent_DelegatesToCorrectHandler()
    {
        var handlerMock = new Mock<IEventHandler>();
        var portfolioMock = new Mock<IPortfolio>();
        var handlers = new Dictionary<Type, IEventHandler>
        {
            [typeof(MarketEvent)] = handlerMock.Object
        };

        var processor = new EventProcessor(portfolioMock.Object, handlers);
        var marketEvent = CreateMarketEvent();

        await processor.ProcessEventAsync(marketEvent, CancellationToken.None);

        handlerMock.Verify(h => h.HandleEventAsync(
            portfolioMock.Object, marketEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_OrderEvent_DelegatesToCorrectHandler()
    {
        var orderHandler = new Mock<IEventHandler>();
        var portfolioMock = new Mock<IPortfolio>();
        var handlers = new Dictionary<Type, IEventHandler>
        {
            [typeof(OrderEvent)] = orderHandler.Object
        };

        var processor = new EventProcessor(portfolioMock.Object, handlers);
        var orderEvent = new OrderEvent(s_testDate, "TestStrategy", s_testAsset,
            TradeAction.Buy, OrderType.Market, 100, 185.50m, null);

        await processor.ProcessEventAsync(orderEvent, CancellationToken.None);

        orderHandler.Verify(h => h.HandleEventAsync(
            portfolioMock.Object, orderEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_FillEvent_DelegatesToCorrectHandler()
    {
        var fillHandler = new Mock<IEventHandler>();
        var portfolioMock = new Mock<IPortfolio>();
        var handlers = new Dictionary<Type, IEventHandler>
        {
            [typeof(FillEvent)] = fillHandler.Object
        };

        var processor = new EventProcessor(portfolioMock.Object, handlers);
        var fillEvent = new FillEvent(s_testDate, s_testAsset, "TestStrategy",
            TradeAction.Buy, 185.50m, 100, 10m);

        await processor.ProcessEventAsync(fillEvent, CancellationToken.None);

        fillHandler.Verify(h => h.HandleEventAsync(
            portfolioMock.Object, fillEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_UnregisteredEventType_ThrowsNotSupportedException()
    {
        var portfolioMock = new Mock<IPortfolio>();
        var handlers = new Dictionary<Type, IEventHandler>();

        var processor = new EventProcessor(portfolioMock.Object, handlers);
        var marketEvent = CreateMarketEvent();

        var act = () => processor.ProcessEventAsync(marketEvent, CancellationToken.None);
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*MarketEvent*");
    }

    [Fact]
    public async Task ProcessEventAsync_CancellationToken_ThrowsOperationCanceled()
    {
        var portfolioMock = new Mock<IPortfolio>();
        var handlers = new Dictionary<Type, IEventHandler>();

        var processor = new EventProcessor(portfolioMock.Object, handlers);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var marketEvent = CreateMarketEvent();

        var act = () => processor.ProcessEventAsync(marketEvent, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProcessEventAsync_MultipleHandlers_RoutesCorrectly()
    {
        var marketHandler = new Mock<IEventHandler>();
        var orderHandler = new Mock<IEventHandler>();
        var portfolioMock = new Mock<IPortfolio>();
        var handlers = new Dictionary<Type, IEventHandler>
        {
            [typeof(MarketEvent)] = marketHandler.Object,
            [typeof(OrderEvent)] = orderHandler.Object,
        };

        var processor = new EventProcessor(portfolioMock.Object, handlers);
        var marketEvent = CreateMarketEvent();

        await processor.ProcessEventAsync(marketEvent, CancellationToken.None);

        marketHandler.Verify(h => h.HandleEventAsync(
            portfolioMock.Object, marketEvent, It.IsAny<CancellationToken>()), Times.Once);
        orderHandler.Verify(h => h.HandleEventAsync(
            It.IsAny<IPortfolio>(), It.IsAny<IFinancialEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
