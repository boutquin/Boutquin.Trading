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

using Boutquin.Trading.Application.RiskManagement;

namespace Boutquin.Trading.Tests.UnitTests.Application;

public sealed class CausalRiskNavTests
{
    private static readonly Symbol s_asset = new("AAA");
    private static readonly DateOnly s_date = new(2026, 4, 1);

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 200000)]
    [InlineData(false, true, 200000)]
    [InlineData(true, false, 200000)]
    [InlineData(true, true, 200000)]
    public void Evaluate_UsesCurrentHoldingsAndCurrencyCash_IndependentOfReporting(bool sector, bool cad, int staleEquity)
    {
        var portfolio = CreatePortfolio(cad);
        if (staleEquity > 0)
        {
            portfolio.EquityCurve[s_date.AddDays(-1)] = staleEquity;
        }
        var before = portfolio.EquityCurve.ToArray();
        // 300 held shares + 500 proposed, against 100,000 USD economic NAV (130,000 CAD).
        var order = Buy(s_date, 500);
        var rule = Rule(sector);
        rule.Evaluate(order, portfolio).IsAllowed.Should().BeFalse();
        rule.EvaluateBatch([order], portfolio).IsAllowed.Should().BeFalse();
        // 300 + 200 is admissible against the identical NAV.
        rule.Evaluate(Buy(s_date, 200), portfolio).IsAllowed.Should().BeTrue();
        rule.EvaluateBatch([Buy(s_date, 200)], portfolio).IsAllowed.Should().BeTrue();
        portfolio.EquityCurve.Should().Equal(before);
        portfolio.GetStrategy("main").Positions[s_asset].Should().Be(300);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evaluate_FutureMarketAndFxTailCannotChangeEarlierDecision(bool sector)
    {
        var portfolio = CreatePortfolio(true);
        portfolio.UpdateHistoricalData(new MarketEvent(s_date.AddDays(1),
            new() { [s_asset] = BarAt(s_date.AddDays(1), 10m) },
            new() { [Currency.USD] = .5m }));
        portfolio.EquityCurve[s_date.AddDays(1)] = 1000000m;
        var rule = Rule(sector);
        rule.Evaluate(Buy(s_date, 500), portfolio).IsAllowed.Should().BeFalse();
        rule.EvaluateBatch([Buy(s_date, 500)], portfolio).IsAllowed.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvaluateBatch_MixedDecisionDates_FailsClosed(bool sector)
    {
        var portfolio = CreatePortfolio(false);
        var result = Rule(sector).EvaluateBatch([Buy(s_date, 1), Buy(s_date.AddDays(1), 1)], portfolio);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("date");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evaluate_NonpositiveCurrentNav_FailsClosed(bool sector)
    {
        var portfolio = CreatePortfolio(false);
        portfolio.GetStrategy("main").UpdateCash(Currency.USD, -100000m);
        portfolio.EquityCurve[s_date] = 100000m;
        Action single = () => Rule(sector).Evaluate(Buy(s_date, 1), portfolio);
        Action batch = () => Rule(sector).EvaluateBatch([Buy(s_date, 1)], portfolio);
        single.Should().Throw<InvalidOperationException>().WithMessage("*NAV*2026-04-01*");
        batch.Should().Throw<InvalidOperationException>().WithMessage("*NAV*2026-04-01*");
    }

    [Fact]
    public async Task CheckAsync_NoReportingRows_TracksPeakBreachAndCooldown()
    {
        var portfolio = CreatePortfolio(false);
        var breaker = new DrawdownCircuitBreaker(.1m, 2);
        await breaker.CheckAsync(portfolio, s_date);
        // The held 300 shares lose 15,000; cash is unchanged. 15% drawdown breaches 10%.
        portfolio.UpdateHistoricalData(new MarketEvent(s_date.AddDays(1),
            new() { [s_asset] = BarAt(s_date.AddDays(1), 50m) }, new()));
        await breaker.CheckAsync(portfolio, s_date.AddDays(1));
        breaker.LiquidationCount.Should().Be(1);
        breaker.InCashMode.Should().BeTrue();
        // Confirm an actual next-bar liquidation through the brokerage and registered fill handler.
        var bar = new SortedDictionary<Symbol, Bar> { [s_asset] = BarAt(s_date.AddDays(2), 50m) };
        await portfolio.ProcessPendingOrdersAsync(s_date.AddDays(2), bar, new());
        portfolio.UpdateHistoricalData(new MarketEvent(s_date.AddDays(2), bar, new()));
        portfolio.GetStrategy("main").Positions[s_asset].Should().Be(0);
        await breaker.CheckAsync(portfolio, s_date.AddDays(2));
        breaker.InCashMode.Should().BeTrue();
        portfolio.UpdateHistoricalData(new MarketEvent(s_date.AddDays(3),
            new() { [s_asset] = BarAt(s_date.AddDays(3), 50m) }, new()));
        await breaker.CheckAsync(portfolio, s_date.AddDays(3));
        breaker.InCashMode.Should().BeFalse();
        breaker.LiquidationCount.Should().Be(1);
        portfolio.EquityCurve.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_NoReportingRows_UnfilledLiquidationRemainsNonResumable()
    {
        var portfolio = CreatePortfolio(false);
        var breaker = new DrawdownCircuitBreaker(.1m, 2);
        await breaker.CheckAsync(portfolio, s_date);
        portfolio.UpdateHistoricalData(new MarketEvent(s_date.AddDays(1),
            new() { [s_asset] = BarAt(s_date.AddDays(1), 50m) }, new()));
        await breaker.CheckAsync(portfolio, s_date.AddDays(1));
        // A missing next-session asset bar expires the day order; it must not claim cash.
        await portfolio.ProcessPendingOrdersAsync(s_date.AddDays(2), new(), new());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.CheckAsync(portfolio, s_date.AddDays(2)));
        error.Message.Should().Contain("incomplete").And.Contain("AAA");
        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.CheckAsync(portfolio, s_date.AddDays(3)));
        portfolio.EquityCurve.Should().BeEmpty();
    }

    private static IRiskRule Rule(bool sector) => sector
        ? new MaxSectorExposureRule(.6m, new Dictionary<Symbol, AssetClassCode> { [s_asset] = AssetClassCode.Equities })
        : new MaxPositionSizeRule(.6m);

    private static Order Buy(DateOnly date, int quantity) => new(date, "main", s_asset, TradeAction.Buy, OrderType.Market, quantity);
    private static Bar BarAt(DateOnly date, decimal price) => new(date, price, price, price, price, price, 1000000);

    private static Portfolio CreatePortfolio(bool cad)
    {
        var baseCurrency = cad ? Currency.CAD : Currency.USD;
        var assets = new Dictionary<Symbol, Currency> { [s_asset] = Currency.USD };
        // Both base and foreign cash matter: 35,000 USD plus 45,500 CAD = 70,000 USD.
        var cash = cad ? new SortedDictionary<Currency, decimal> { [Currency.CAD] = 45500m, [Currency.USD] = 35000m }
            : new SortedDictionary<Currency, decimal> { [Currency.USD] = 70000m };
        var strategy = new ConstructionModelStrategy("main", assets, cash,
            new ClosePriceOrderPriceCalculationStrategy(), new DynamicWeightPositionSizer(baseCurrency),
            new Boutquin.Trading.Application.PortfolioConstruction.EqualWeightConstruction(), RebalancingFrequency.Monthly);
        strategy.UpdatePositions(s_asset, 300);
        var handlers = new Dictionary<Type, IEventHandler> { [typeof(FillEvent)] = new FillEventHandler() };
        var portfolio = new Portfolio(baseCurrency, new Dictionary<string, IStrategy> { ["main"] = strategy }, assets,
            handlers, new SimulatedBrokerage(new Boutquin.Trading.Application.CostModels.PercentageOfValueCostModel(.001m)));
        portfolio.UpdateHistoricalData(new MarketEvent(s_date, new() { [s_asset] = BarAt(s_date, 100m) },
            cad ? new() { [Currency.USD] = 1.3m } : new()));
        return portfolio;
    }
}
