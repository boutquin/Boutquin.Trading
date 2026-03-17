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

using Boutquin.Trading.Application;

public sealed class CancellationTokenTests
{
    private static readonly Asset s_vti = new("VTI");

    [Fact]
    public async Task BackTest_RunAsync_CancelledToken_ShouldThrowOperationCancelledException()
    {
        // Arrange: create a minimal backtest setup
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Name).Returns("Test");
        strategyMock.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode> { [s_vti] = CurrencyCode.USD });
        strategyMock.Setup(s => s.Positions).Returns(new Dictionary<Asset, int>());
        strategyMock.Setup(s => s.Cash).Returns(new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 100_000m });

        var portfolioMock = new Mock<IPortfolio>();
        portfolioMock.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { ["Test"] = strategyMock.Object });
        portfolioMock.Setup(p => p.BaseCurrency).Returns(CurrencyCode.USD);
        portfolioMock.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        var benchmarkMock = new Mock<IPortfolio>();
        benchmarkMock.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { ["Bench"] = strategyMock.Object });
        benchmarkMock.Setup(p => p.BaseCurrency).Returns(CurrencyCode.USD);
        benchmarkMock.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        // Market data that yields data slowly
        var marketData = CreateMarketDataStream();
        var fxData = CreateEmptyFxStream();

        var fetcherMock = new Mock<IMarketDataFetcher>();
        fetcherMock.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
                   .Returns(marketData);
        fetcherMock.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                   .Returns(fxData);

        var backtest = new BackTest(portfolioMock.Object, benchmarkMock.Object, fetcherMock.Object, CurrencyCode.USD);

        // Act: cancel immediately
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Assert
        var act = () => backtest.RunAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Portfolio_HandleEventAsync_CancelledToken_ShouldThrowOperationCancelledException()
    {
        // Arrange
        var handlerMock = new Mock<IEventHandler>();
        handlerMock.Setup(h => h.HandleEventAsync(It.IsAny<IPortfolio>(), It.IsAny<IFinancialEvent>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Name).Returns("Test");
        strategyMock.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode> { [s_vti] = CurrencyCode.USD });
        strategyMock.Setup(s => s.Positions).Returns(new Dictionary<Asset, int>());

        var brokerMock = new Mock<IBrokerage>();

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new Dictionary<string, IStrategy> { ["Test"] = strategyMock.Object },
            new Dictionary<Asset, CurrencyCode> { [s_vti] = CurrencyCode.USD },
            new Dictionary<Type, IEventHandler> { [typeof(MarketEvent)] = handlerMock.Object },
            brokerMock.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var marketEvent = new MarketEvent(
            new DateOnly(2026, 1, 1),
            new SortedDictionary<Asset, MarketData>(),
            new SortedDictionary<CurrencyCode, decimal>());

        // Act & Assert
        var act = () => portfolio.HandleEventAsync(marketEvent, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Portfolio_SubmitOrderAsync_CancelledToken_ShouldThrowOperationCancelledException()
    {
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Name).Returns("Test");
        strategyMock.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode> { [s_vti] = CurrencyCode.USD });
        strategyMock.Setup(s => s.Positions).Returns(new Dictionary<Asset, int>());

        var brokerMock = new Mock<IBrokerage>();
        brokerMock.Setup(b => b.SubmitOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true);

        var handlerMock = new Mock<IEventHandler>();
        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new Dictionary<string, IStrategy> { ["Test"] = strategyMock.Object },
            new Dictionary<Asset, CurrencyCode> { [s_vti] = CurrencyCode.USD },
            new Dictionary<Type, IEventHandler> { [typeof(MarketEvent)] = handlerMock.Object },
            brokerMock.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var orderEvent = new OrderEvent(
            new DateOnly(2026, 1, 1),
            "Test",
            s_vti,
            TradeAction.Buy,
            OrderType.Market,
            100);

        var act = () => portfolio.SubmitOrderAsync(orderEvent, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> CreateMarketDataStream()
    {
        for (var i = 0; i < 252; i++)
        {
            var date = new DateOnly(2026, 1, 1).AddDays(i);
            yield return new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(
                date,
                new SortedDictionary<Asset, MarketData>
                {
                    [s_vti] = new MarketData(date, 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
                });
        }
    }

    private static async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> CreateEmptyFxStream()
    {
        yield break;
    }
#pragma warning restore CS1998
}
