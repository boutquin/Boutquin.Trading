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

namespace Boutquin.Trading.Tests.UnitTests.Application.Calendar;

using Boutquin.Trading.Application.Brokers;
using Boutquin.Trading.Application.CostModels;
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tests for trading calendar integration with SimulatedBrokerage fill warnings.
/// </summary>
public sealed class SimulatedBrokerageCalendarTests
{
    private static readonly Asset s_aapl = new("AAPL");

    private static SortedDictionary<Asset, MarketData> CreateDayData(DateOnly date) =>
        new()
        {
            [s_aapl] = new(date, 100m, 105m, 95m, 100m, 100m, 1_000_000L, 0m, 1m),
        };

    [Fact]
    public async Task ProcessPendingOrders_NoCalendar_ProcessesNormally()
    {
        var mockFetcher = new Mock<IMarketDataFetcher>();
        var brokerage = new SimulatedBrokerage(mockFetcher.Object, new PercentageOfValueCostModel(0.001m));

        var order = new Order(new DateOnly(2025, 1, 6), "test", s_aapl, TradeAction.Buy, OrderType.Market, 10);
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);

        // Should process without error — no calendar, no warning
        await brokerage.ProcessPendingOrdersAsync(
            new DateOnly(2025, 1, 7),
            CreateDayData(new DateOnly(2025, 1, 7)),
            CancellationToken.None).ConfigureAwait(true);
    }

    [Fact]
    public async Task ProcessPendingOrders_TradingDay_NoWarning()
    {
        var mockCalendar = new Mock<ITradingCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(It.IsAny<DateOnly>())).Returns(true);

        var mockLogger = new Mock<ILogger<SimulatedBrokerage>>();
        var mockFetcher = new Mock<IMarketDataFetcher>();

        var brokerage = new SimulatedBrokerage(
            mockFetcher.Object,
            new PercentageOfValueCostModel(0.001m),
            tradingCalendar: mockCalendar.Object,
            logger: mockLogger.Object);

        var order = new Order(new DateOnly(2025, 1, 6), "test", s_aapl, TradeAction.Buy, OrderType.Market, 10);
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);

        await brokerage.ProcessPendingOrdersAsync(
            new DateOnly(2025, 1, 7),
            CreateDayData(new DateOnly(2025, 1, 7)),
            CancellationToken.None).ConfigureAwait(true);

        // Verify no warning was logged
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPendingOrders_NonTradingDay_LogsWarning()
    {
        var mockCalendar = new Mock<ITradingCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(It.IsAny<DateOnly>())).Returns(false);

        var mockLogger = new Mock<ILogger<SimulatedBrokerage>>();
        var mockFetcher = new Mock<IMarketDataFetcher>();

        var brokerage = new SimulatedBrokerage(
            mockFetcher.Object,
            new PercentageOfValueCostModel(0.001m),
            tradingCalendar: mockCalendar.Object,
            logger: mockLogger.Object);

        var order = new Order(new DateOnly(2025, 1, 4), "test", s_aapl, TradeAction.Buy, OrderType.Market, 10);
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);

        await brokerage.ProcessPendingOrdersAsync(
            new DateOnly(2025, 1, 5), // Sunday
            CreateDayData(new DateOnly(2025, 1, 5)),
            CancellationToken.None).ConfigureAwait(true);

        // Verify warning was logged
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingOrders_NonTradingDay_SkipsProcessing()
    {
        var mockCalendar = new Mock<ITradingCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(It.IsAny<DateOnly>())).Returns(false);

        var mockFetcher = new Mock<IMarketDataFetcher>();
        var brokerage = new SimulatedBrokerage(
            mockFetcher.Object,
            new PercentageOfValueCostModel(0.001m),
            tradingCalendar: mockCalendar.Object);

        FillEvent? receivedFill = null;
        brokerage.FillOccurred += (_, fill) =>
        {
            receivedFill = fill;
            return Task.CompletedTask;
        };

        var order = new Order(new DateOnly(2025, 1, 4), "test", s_aapl, TradeAction.Buy, OrderType.Market, 10);
        await brokerage.SubmitOrderAsync(order, CancellationToken.None).ConfigureAwait(true);

        await brokerage.ProcessPendingOrdersAsync(
            new DateOnly(2025, 1, 5),
            CreateDayData(new DateOnly(2025, 1, 5)),
            CancellationToken.None).ConfigureAwait(true);

        // Non-trading day: orders should NOT fill — they carry over to next trading day
        receivedFill.Should().BeNull("orders should not fill on non-trading days");
    }
}
