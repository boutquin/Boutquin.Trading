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

using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.Trading.Application.RiskManagement;
using Boutquin.Trading.Recipes.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

public sealed class BacktestDrawdownExecutionTests
{
    private static readonly Symbol s_asset = new("AAA");
    private static readonly UsNyseCalendar s_calendar = new();

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(1, false, true)]
    [InlineData(1, true, true)]
    [InlineData(2, false, false)]
    [InlineData(1, true, false)]
    public async Task RunAsync_DrawdownDominatesQuarterlyRebalanceAndCooldown(int crashDay, bool resume, bool burnIn)
    {
        var main = Portfolio("main");
        var benchmark = Portfolio("benchmark");
        var breaker = new DrawdownCircuitBreaker(.18m, 20);
        var crash = new DateOnly(2026, 4, crashDay);
        var end = resume ? new DateOnly(2026, 5, 5) : new DateOnly(2026, 4, 8);
        var backtest = new BackTest(main, benchmark, Currency.USD, NullLogger<BackTest>.Instance, 0m, breaker, s_calendar, 0m);
        var observedCashDays = 0;
        Task Run() => backtest.RunAsync(Data(crash, end), CancellationToken.None, afterDayCallback: (p, _) =>
        {
            var date = p.HistoricalMarketData.Keys.Last();
            if (burnIn && date <= new DateOnly(2026, 4, 6))
            {
                p.EquityCurve.Should().BeEmpty();
            }
            var quantity = p.GetStrategy("main").Positions.GetValueOrDefault(s_asset);
            // Includes same-day liquidation versus a .25 exposure rebalance: the old loop queued
            // both sell batches and could create a short position when they filled together.
            quantity.Should().BeGreaterThanOrEqualTo(0);
            if (date > crash && breaker.InCashMode)
            {
                quantity.Should().Be(0);
                observedCashDays++;
            }
            p.HistoricalMarketData.Keys.Last().Should().Be(date);
            if (burnIn && date == end)
            {
                throw new OperationCanceledException("Invented execution witness complete before metric analysis.");
            }
            return Task.CompletedTask;
        }, burnInEndDate: burnIn ? new DateOnly(2026, 4, 6) : null);
        if (burnIn)
        {
            await Assert.ThrowsAsync<OperationCanceledException>(Run);
        }
        else
        {
            await Run();
        }
        if (burnIn)
        {
            main.EquityCurve.Keys.Should().Equal(Data(crash, end).Prices.Keys.Where(date => date > new DateOnly(2026, 4, 6)));
        }
        breaker.LiquidationCount.Should().Be(1);
        observedCashDays.Should().BeGreaterThan(0);
        breaker.InCashMode.Should().Be(!resume);
        if (resume)
        {
            main.GetStrategy("main").Positions[s_asset].Should().BeGreaterThan(0);
        }
        else
        {
            main.GetStrategy("main").Positions[s_asset].Should().Be(0);
        }
    }

    [Fact]
    public async Task RunAsync_UntriggeredControlPreservesExpenseAndEquityAccounting()
    {
        static async Task<SortedDictionary<DateOnly, decimal>> Run(bool controlled)
        {
            var main = Portfolio("main");
            var benchmark = Portfolio("benchmark");
            var backtest = new BackTest(main, benchmark, Currency.USD, NullLogger<BackTest>.Instance, 0m,
                controlled ? new DrawdownCircuitBreaker(.99m, 20) : null, s_calendar, 50m);
            await backtest.RunAsync(Data(new DateOnly(2030, 1, 1), new DateOnly(2026, 5, 5)), CancellationToken.None);
            return main.EquityCurve;
        }
        (await Run(true)).Should().Equal(await Run(false));
    }

    [Fact]
    public async Task RunAsync_UnfilledLiquidationStopsBeforeClaimingCashCooldown()
    {
        var main = Portfolio("main");
        var breaker = new DrawdownCircuitBreaker(.18m, 20);
        var data = Data(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 8));
        data.Prices[new DateOnly(2026, 4, 2)].Remove(s_asset);
        var backtest = new BackTest(main, Portfolio("benchmark"), Currency.USD,
            NullLogger<BackTest>.Instance, 0m, breaker, s_calendar, 0m);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => backtest.RunAsync(data, CancellationToken.None));
        error.Message.Should().Contain("liquidation").And.Contain("AAA").And.Contain("2026-04-02");
        main.GetStrategy("main").Positions[s_asset].Should().BeGreaterThan(0);
        main.HistoricalMarketData.Keys.Last().Should().Be(new DateOnly(2026, 4, 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckAsync_InterruptedLiquidationFailsWithoutReplayingAcceptedOrders(bool cancelled)
    {
        var portfolio = new Mock<IPortfolio>();
        var strategy = new Mock<IStrategy>();
        strategy.Setup(x => x.Positions).Returns(new Dictionary<Symbol, int> { [s_asset] = 10, [new Symbol("BBB")] = 20 });
        portfolio.Setup(x => x.Strategies).Returns(new Dictionary<string, IStrategy> { ["main"] = strategy.Object });
        portfolio.Setup(x => x.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());
        portfolio.Setup(x => x.CalculateTotalPortfolioValue(It.IsAny<DateOnly>()))
            .Returns((DateOnly date) => date == new DateOnly(2026, 4, 1) ? 100m : 70m);
        var calls = 0;
        portfolio.Setup(x => x.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++calls == 1 ? Task.FromResult(true) : cancelled
                ? Task.FromException<bool>(new OperationCanceledException()) : Task.FromResult(false));
        var breaker = new DrawdownCircuitBreaker(.18m, 20);
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 4, 1));
        var error = await Record.ExceptionAsync(() => breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 4, 2)));
        error.Should().NotBeNull();
        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 4, 2)));
        calls.Should().Be(2);
        breaker.LiquidationCount.Should().Be(0);
        breaker.InCashMode.Should().BeFalse();
    }

    private static Portfolio Portfolio(string name)
    {
        var assets = new Dictionary<Symbol, Currency> { [s_asset] = Currency.USD };
        var strategy = new ConstructionModelStrategy(name, assets,
            new SortedDictionary<Currency, decimal> { [Currency.USD] = 100000m },
            new ClosePriceOrderPriceCalculationStrategy(), new DynamicWeightPositionSizer(Currency.USD),
            new ScheduledExposure(), RebalancingFrequency.Quarterly);
        var handlers = new Dictionary<Type, IEventHandler>
        {
            [typeof(MarketEvent)] = new MarketEventHandler(),
            [typeof(OrderEvent)] = new OrderEventHandler(),
            [typeof(SignalEvent)] = new RebalancingSignalEventHandler(Currency.USD),
            [typeof(FillEvent)] = new FillEventHandler(),
            [typeof(FxOrderEvent)] = new FxOrderEventHandler(),
            [typeof(FxFillEvent)] = new FxFillEventHandler(),
        };
        return new Portfolio(Currency.USD, new Dictionary<string, IStrategy> { [name] = strategy }, assets,
            handlers, new SimulatedBrokerage(new Boutquin.Trading.Application.CostModels.PercentageOfValueCostModel(.001m),
                tradingCalendar: s_calendar));
    }

    private static FakeBacktestDataset Data(DateOnly crash, DateOnly end)
    {
        var dataset = new FakeBacktestDataset();
        for (var date = new DateOnly(2026, 1, 2); date <= end; date = date.AddDays(1))
        {
            if (!s_calendar.IsBusinessDay(date))
            {
                continue;
            }

            var price = (date < crash ? 100m : 60m) + (date.DayNumber % 2 == 0 ? .1m : 0m);
            dataset.Prices[date] = new SortedDictionary<Symbol, Bar>
            { [s_asset] = new Bar(date, price, price, price, price, price, 1000000) };
            dataset.FxRates[date] = new SortedDictionary<Currency, decimal> { [Currency.USD] = 1m };
        }
        return dataset;
    }

    private sealed class ScheduledExposure : ILeveragedConstructionModel
    {
        public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(IReadOnlyList<Symbol> assets, decimal[][] returns)
            => assets.ToDictionary(a => a, _ => 1m);
        public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(IReadOnlyList<Symbol> assets, decimal[][] returns, DateOnly date)
            => assets.ToDictionary(a => a, _ => date >= new DateOnly(2026, 4, 2) ? .25m : 1m);
    }
}
