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

using Boutquin.Domain.Extensions;

using Money = Boutquin.Domain.ValueObjects.Money;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// M-9 (M9-BND) tests for the additive <see cref="Money"/> boundary APIs on the
/// portfolio/strategy construction + reporting surfaces. These pin AC-4's additive-boundary
/// portion: a <see cref="Money"/>-returning total carries the base currency; the
/// <see cref="Money"/> companion projections (<c>EquityCurveAsMoney</c>, <c>CashAsMoney</c>)
/// equal the <see cref="decimal"/> backing collections amount-for-amount with the correct
/// currency tag; and the <see cref="Money"/>-accepting <c>UpdateCash</c> overloads route a
/// right-currency amount to the <see cref="decimal"/> core while rejecting a wrong-currency
/// amount with <see cref="CurrencyMismatchException"/> (never silently crediting the wrong
/// bucket).
/// </summary>
public sealed class MoneyBoundaryApiTests
{
    private static readonly Symbol s_spy = new("SPY");
    private static readonly DateOnly s_date = new(2024, 1, 2);

    private static TestStrategy CashOnlyStrategy(string name, params (Currency Currency, decimal Amount)[] cash)
    {
        var dict = new SortedDictionary<Currency, decimal>();
        foreach (var (currency, amount) in cash)
        {
            dict[currency] = amount;
        }

        return new TestStrategy
        {
            Name = name,
            // No assets: the cash-only fixture isolates the Money projections/routing from
            // market-data/FX lookups, so ComputeTotalValue sums cash buckets only.
            Assets = new Dictionary<Symbol, Currency>(),
            Cash = dict,
        };
    }

    private static Portfolio BuildPortfolio(Currency baseCurrency, IStrategy strategy)
    {
        var assetCurrencies = new Dictionary<Symbol, Currency> { { s_spy, Currency.USD } };
        var handlers = new Dictionary<Type, IEventHandler>
        {
            { typeof(MarketEvent), new MarketEventHandler() },
        };
        var broker = new Mock<IBrokerage>().Object;

        var portfolio = new Portfolio(
            baseCurrency,
            new Dictionary<string, IStrategy> { { strategy.Name, strategy } },
            assetCurrencies,
            handlers,
            broker);

        // Seed the (otherwise empty) historical dictionaries so ComputeTotalValue's
        // non-empty-dictionary guard is satisfied. The strategy holds no assets, so the
        // total is cash-only regardless of the seeded bar.
        portfolio.UpdateHistoricalData(new MarketEvent(
            s_date,
            new SortedDictionary<Symbol, Bar>(),
            new SortedDictionary<Currency, decimal>()));

        return portfolio;
    }

    // ---- AC-4 (additive) RED #1: Money-returning total carries BaseCurrency ----

    /// <summary>
    /// <see cref="IPortfolio.CalculateTotalPortfolioValueAsMoney"/> tags the existing decimal
    /// total with the portfolio's <see cref="IPortfolio.BaseCurrency"/>.
    /// </summary>
    [Fact]
    public void CalculateTotalPortfolioValueAsMoney_TagsTotalWithBaseCurrency()
    {
        IStrategy strategy = CashOnlyStrategy("S", (Currency.USD, 12_345m));
        IPortfolio portfolio = BuildPortfolio(Currency.USD, strategy);

        var decimalTotal = portfolio.CalculateTotalPortfolioValue(s_date);
        var moneyTotal = portfolio.CalculateTotalPortfolioValueAsMoney(s_date);

        moneyTotal.Amount.Should().Be(decimalTotal);
        moneyTotal.Currency.Should().Be(Currency.USD);
    }

    /// <summary>
    /// <see cref="IStrategy.ComputeTotalValueAsMoney"/> tags the existing decimal total with the
    /// supplied base currency.
    /// </summary>
    [Fact]
    public void ComputeTotalValueAsMoney_TagsTotalWithBaseCurrency()
    {
        IStrategy strategy = CashOnlyStrategy("S", (Currency.USD, 9_000m));
        var hmd = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            { s_date, new SortedDictionary<Symbol, Bar>() },
        };
        var fx = new Dictionary<DateOnly, SortedDictionary<Currency, decimal>>
        {
            { s_date, new SortedDictionary<Currency, decimal>() },
        };

        var decimalTotal = strategy.ComputeTotalValue(s_date, Currency.USD, hmd, fx);
        var moneyTotal = strategy.ComputeTotalValueAsMoney(s_date, Currency.USD, hmd, fx);

        moneyTotal.Amount.Should().Be(decimalTotal);
        moneyTotal.Currency.Should().Be(Currency.USD);
    }

    // ---- AC-4 (additive) RED #2: UpdateCash(Money) routing + rejection ----

    /// <summary>
    /// <see cref="IStrategy.UpdateCash(Money)"/> with a right-currency <see cref="Money"/> routes
    /// to the decimal core: the bucket balance changes by the money amount.
    /// </summary>
    [Fact]
    public void StrategyUpdateCashMoney_RightCurrency_RoutesToDecimalCore()
    {
        var strategy = CashOnlyStrategy("S", (Currency.USD, 1_000m));

        // The Money overload is a default interface method — dispatched via IStrategy.
        ((IStrategy)strategy).UpdateCash(Currency.USD.Amount(250m));

        strategy.Cash[Currency.USD].Should().Be(1_250m);
    }

    /// <summary>
    /// <see cref="IStrategy.UpdateCash(Money)"/> with a wrong-currency <see cref="Money"/> (no such
    /// bucket) throws <see cref="CurrencyMismatchException"/> rather than silently creating a new
    /// bucket / crediting the wrong one.
    /// </summary>
    [Fact]
    public void StrategyUpdateCashMoney_WrongCurrency_ThrowsCurrencyMismatch()
    {
        var strategy = CashOnlyStrategy("S", (Currency.USD, 1_000m));

        var act = () => ((IStrategy)strategy).UpdateCash(Currency.CAD.Amount(250m));

        act.Should().Throw<CurrencyMismatchException>();
        strategy.Cash.Should().NotContainKey(Currency.CAD);
        strategy.Cash[Currency.USD].Should().Be(1_000m);
    }

    /// <summary>
    /// <see cref="IPortfolio.UpdateCash(string, Money)"/> with a right-currency <see cref="Money"/>
    /// routes to the decimal core of the named strategy.
    /// </summary>
    [Fact]
    public void PortfolioUpdateCashMoney_RightCurrency_RoutesToDecimalCore()
    {
        var strategy = CashOnlyStrategy("S", (Currency.USD, 1_000m));
        IPortfolio portfolio = BuildPortfolio(Currency.USD, strategy);

        portfolio.UpdateCash("S", Currency.USD.Amount(500m));

        strategy.Cash[Currency.USD].Should().Be(1_500m);
    }

    /// <summary>
    /// <see cref="IPortfolio.UpdateCash(string, Money)"/> with a wrong-currency <see cref="Money"/>
    /// throws <see cref="CurrencyMismatchException"/> and never credits the wrong bucket.
    /// </summary>
    [Fact]
    public void PortfolioUpdateCashMoney_WrongCurrency_ThrowsCurrencyMismatch()
    {
        var strategy = CashOnlyStrategy("S", (Currency.USD, 1_000m));
        IPortfolio portfolio = BuildPortfolio(Currency.USD, strategy);

        var act = () => portfolio.UpdateCash("S", Currency.CAD.Amount(500m));

        act.Should().Throw<CurrencyMismatchException>();
        strategy.Cash.Should().NotContainKey(Currency.CAD);
        strategy.Cash[Currency.USD].Should().Be(1_000m);
    }

    // ---- AC-4 (additive) RED #3: Money companion projections ----

    /// <summary>
    /// <see cref="IStrategy.CashAsMoney"/> equals the backing <see cref="IStrategy.Cash"/> decimal
    /// collection amount-for-amount, each value tagged with its own bucket currency.
    /// </summary>
    [Fact]
    public void CashAsMoney_EqualsBackingCash_TaggedWithOwnBucketCurrency()
    {
        IStrategy strategy = CashOnlyStrategy("S", (Currency.USD, 1_000m), (Currency.CAD, 2_500m));

        var asMoney = strategy.CashAsMoney;

        asMoney.Should().HaveCount(strategy.Cash.Count);
        foreach (var (currency, amount) in strategy.Cash)
        {
            asMoney[currency].Amount.Should().Be(amount);
            asMoney[currency].Currency.Should().Be(currency);
        }
    }

    /// <summary>
    /// <see cref="IPortfolio.EquityCurveAsMoney"/> equals the backing
    /// <see cref="IPortfolio.EquityCurve"/> decimal collection amount-for-amount, each value tagged
    /// with the portfolio's <see cref="IPortfolio.BaseCurrency"/>.
    /// </summary>
    [Fact]
    public void EquityCurveAsMoney_EqualsBackingEquityCurve_TaggedWithBaseCurrency()
    {
        var strategy = CashOnlyStrategy("S", (Currency.USD, 10_000m));
        IPortfolio portfolio = BuildPortfolio(Currency.USD, strategy);

        // Populate the backing decimal equity curve via the production path.
        portfolio.UpdateEquityCurve(s_date);
        portfolio.UpdateEquityCurve(s_date.AddDays(1));

        var asMoney = portfolio.EquityCurveAsMoney;

        asMoney.Should().HaveCount(portfolio.EquityCurve.Count);
        foreach (var (date, amount) in portfolio.EquityCurve)
        {
            asMoney[date].Amount.Should().Be(amount);
            asMoney[date].Currency.Should().Be(Currency.USD);
        }
    }
}
