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

using Boutquin.Trading.Recipes.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Tests for trading calendar integration with the BackTest engine.
/// </summary>
public sealed class BacktestCalendarTests
{
    private static readonly Symbol s_aapl = new("AAPL");

    [Fact]
    public void Constructor_BackwardCompatible()
    {
        // All existing constructor overloads still work
        var equity = new[] { 10000m, 10100m };
        var mockPortfolio = CreateMockPortfolio(equity);
        var mockBenchmark = CreateMockPortfolio(equity);

        // 3-param
        var bt1 = new BackTest(mockPortfolio.Object, mockBenchmark.Object, CurrencyCode.USD);
        bt1.Should().NotBeNull();

        // 4-param (with logger)
        var bt2 = new BackTest(mockPortfolio.Object, mockBenchmark.Object, CurrencyCode.USD, NullLogger<BackTest>.Instance);
        bt2.Should().NotBeNull();

        // 5-param (with dailyRiskFreeRate)
        var bt3 = new BackTest(mockPortfolio.Object, mockBenchmark.Object, CurrencyCode.USD, NullLogger<BackTest>.Instance, 0m);
        bt3.Should().NotBeNull();

        // 6-param (with drawdownControl)
        var bt4 = new BackTest(mockPortfolio.Object, mockBenchmark.Object, CurrencyCode.USD, NullLogger<BackTest>.Instance, 0m, null);
        bt4.Should().NotBeNull();

        // 7-param (with calendar)
        var mockCalendar = new Mock<IBusinessCalendar>();
        var bt5 = new BackTest(mockPortfolio.Object, mockBenchmark.Object, CurrencyCode.USD, NullLogger<BackTest>.Instance, 0m, null, mockCalendar.Object);
        bt5.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_NoCalendar_ProcessesAllDates()
    {
        // Without calendar, all dates in market data are processed
        var (backtest, portfolio, _, dataset) = CreateBacktestWithMarketData(tradingCalendar: null);

        await backtest.RunAsync(dataset).ConfigureAwait(true);

        // 10 trading days of data provided → 10 equity curve updates
        portfolio.Verify(
            p => p.UpdateEquityCurve(It.IsAny<DateOnly>()),
            Times.Exactly(10));
    }

    [Fact]
    public async Task RunAsync_WithCalendar_SkipsNonTradingDays()
    {
        // Calendar marks 2 dates as non-trading
        var mockCalendar = new Mock<IBusinessCalendar>();
        mockCalendar.Setup(c => c.IsBusinessDay(It.IsAny<DateOnly>())).Returns(true);
        mockCalendar.Setup(c => c.IsBusinessDay(new DateOnly(2025, 1, 4))).Returns(false); // Saturday
        mockCalendar.Setup(c => c.IsBusinessDay(new DateOnly(2025, 1, 5))).Returns(false); // Sunday
        mockCalendar.Setup(c => c.BusinessDaysPerYear).Returns(252);

        var (backtest, portfolio, _, dataset) = CreateBacktestWithMarketData(tradingCalendar: mockCalendar.Object);

        await backtest.RunAsync(dataset).ConfigureAwait(true);

        // 10 dates of data, 2 skipped → 8 equity curve updates
        portfolio.Verify(
            p => p.UpdateEquityCurve(It.IsAny<DateOnly>()),
            Times.Exactly(8));
    }

    [Fact]
    public async Task RunAsync_WithCalendar_AllTradingDays_NoSkips()
    {
        var mockCalendar = new Mock<IBusinessCalendar>();
        mockCalendar.Setup(c => c.IsBusinessDay(It.IsAny<DateOnly>())).Returns(true);
        mockCalendar.Setup(c => c.BusinessDaysPerYear).Returns(252);

        var (backtest, portfolio, _, dataset) = CreateBacktestWithMarketData(tradingCalendar: mockCalendar.Object);

        await backtest.RunAsync(dataset).ConfigureAwait(true);

        // All 10 dates are trading days → same as no calendar
        portfolio.Verify(
            p => p.UpdateEquityCurve(It.IsAny<DateOnly>()),
            Times.Exactly(10));
    }

    private static (BackTest Backtest, Mock<IPortfolio> Portfolio, Mock<IPortfolio> Benchmark, FakeBacktestDataset Dataset) CreateBacktestWithMarketData(
        IBusinessCalendar? tradingCalendar)
    {
        // Different equity curves to avoid zero active-return std dev
        var portfolioEquity = new[] { 10000m, 10050m, 9980m, 10020m, 9960m, 10040m, 10010m, 9990m, 10060m, 10030m };
        var benchmarkEquity = new[] { 10000m, 10020m, 10010m, 10030m, 10005m, 10025m, 10015m, 10035m, 10050m, 10045m };
        var portfolio = CreateMockPortfolio(portfolioEquity);
        var benchmark = CreateMockPortfolio(benchmarkEquity);

        // Create 10 days of market data: Jan 2-13 (skipping gaps)
        var dates = new[]
        {
            new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 3),
            new DateOnly(2025, 1, 4), new DateOnly(2025, 1, 5),
            new DateOnly(2025, 1, 6), new DateOnly(2025, 1, 7),
            new DateOnly(2025, 1, 8), new DateOnly(2025, 1, 9),
            new DateOnly(2025, 1, 10), new DateOnly(2025, 1, 13),
        };

        var prices = new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
        foreach (var date in dates)
        {
            prices[date] = new SortedDictionary<Symbol, Bar>
            {
                [s_aapl] = new(date, 100m, 105m, 95m, 100m, 100m, 1_000_000L),
            };
        }

        var dataset = new FakeBacktestDataset
        {
            Prices = prices,
            FxRates = [],
        };

        var backtest = tradingCalendar is not null
            ? new BackTest(portfolio.Object, benchmark.Object, CurrencyCode.USD,
                NullLogger<BackTest>.Instance, 0m, null, tradingCalendar)
            : new BackTest(portfolio.Object, benchmark.Object, CurrencyCode.USD,
                NullLogger<BackTest>.Instance, 0m, null);

        return (backtest, portfolio, benchmark, dataset);
    }

    private static Mock<IPortfolio> CreateMockPortfolio(decimal[] equityValues)
    {
        var mock = new Mock<IPortfolio>();
        var strategies = new SortedDictionary<string, IStrategy>();
        var equityCurve = new SortedDictionary<DateOnly, decimal>();
        mock.Setup(p => p.Strategies).Returns(strategies);
        mock.Setup(p => p.EquityCurve).Returns(equityCurve);
        mock.Setup(p => p.ProcessPendingOrdersAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<SortedDictionary<Symbol, Bar>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(p => p.HandleEventAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var callCount = 0;
        mock.Setup(p => p.UpdateEquityCurve(It.IsAny<DateOnly>()))
            .Callback<DateOnly>(date =>
            {
                var idx = callCount % equityValues.Length;
                equityCurve[date] = equityValues[idx];
                callCount++;
            });

        return mock;
    }
}
