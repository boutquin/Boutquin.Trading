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

using Boutquin.Trading.Application.CostModels;
using Boutquin.Trading.Application.Helpers;
using Boutquin.Trading.Application.SlippageModels;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Unit coverage of the explicit FX trade pipeline: <see cref="FxFundingPlanner"/>,
/// <see cref="SimulatedBrokerage"/>'s FX fills and fill ordering, <see cref="FxFillEventHandler"/>,
/// <see cref="FxOrderEventHandler"/> and <see cref="SpreadFxCostModel"/>.
/// </summary>
public sealed class FxTradePipelineTests
{
    private static readonly DateOnly s_signalDate = new(2024, 6, 24);
    private static readonly DateOnly s_fillDate = new(2024, 6, 25);
    private static readonly Symbol s_vgsh = new("VGSH");
    private static readonly Symbol s_xbb = new("XBB");

    /// <summary>CAD base: 1 USD = 1.30 CAD, 1 EUR = 1.50 CAD.</summary>
    private static readonly SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>> s_fx = new()
    {
        [s_signalDate] = new SortedDictionary<Currency, decimal> { [Currency.USD] = 1.30m, [Currency.EUR] = 1.50m },
        [s_fillDate] = new SortedDictionary<Currency, decimal> { [Currency.USD] = 1.30m, [Currency.EUR] = 1.50m },
    };

    // ─── FxFundingPlanner ────────────────────────────────────────────────

    [Fact]
    public void Plan_PolicyNone_EmitsNothing()
    {
        var strategy = StrategyWithCash((Currency.CAD, 100_000m));

        var orders = FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.USD, -50_000m)), s_fx, FxFundingPolicy.None);

        orders.Should().BeEmpty();
    }

    [Fact]
    public void Plan_SingleCurrencyPortfolio_EmitsNothing()
    {
        var strategy = StrategyWithCash((Currency.CAD, 100_000m));

        var orders = FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.CAD, -80_000m)), s_fx, FxFundingPolicy.FundShortfall);

        orders.Should().BeEmpty("a base-currency buy funded by base cash needs no conversion");
    }

    [Fact]
    public void Plan_ForeignShortfall_BuysExactlyTheShortfallWithBase()
    {
        // Holds 10,000 USD already; buys 50,000 USD of a US asset → needs 40,000 USD more.
        var strategy = StrategyWithCash((Currency.CAD, 100_000m), (Currency.USD, 10_000m));

        var orders = FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.USD, -50_000m)), s_fx, FxFundingPolicy.FundShortfall);

        var order = orders.Should().ContainSingle().Which;
        order.Should().BeEquivalentTo(new FxOrderEvent(s_signalDate, "S", Currency.CAD, Currency.USD, 40_000m));
    }

    [Fact]
    public void Plan_ForeignSurplusCoversBaseShortfall_SellsForeignForBase()
    {
        // Cross-currency switch: sell 100,000 USD of a US asset, buy 130,000 CAD of a CA asset, no cash.
        var strategy = StrategyWithCash((Currency.CAD, 0m));

        var orders = FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.USD, 100_000m), (Currency.CAD, -130_000m)), s_fx, FxFundingPolicy.FundShortfall);

        var order = orders.Should().ContainSingle().Which;
        order.Should().BeEquivalentTo(new FxOrderEvent(s_signalDate, "S", Currency.USD, Currency.CAD, 130_000m),
            "the USD proceeds (100,000 × 1.30 = 130,000 CAD) fund the CAD buy exactly");
    }

    [Fact]
    public void Plan_BaseShortfallLargerThanSurplus_TakesOnlyTheSurplus()
    {
        var strategy = StrategyWithCash((Currency.CAD, 0m));

        var orders = FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.USD, 10_000m), (Currency.CAD, -130_000m)), s_fx, FxFundingPolicy.FundShortfall);

        orders.Should().ContainSingle().Which.BuyAmount.Should().Be(13_000m,
            "only 10,000 USD (13,000 CAD) is available; the rest of the CAD buy is cash-limited at fill");
    }

    [Fact]
    public void Plan_FundShortfall_LeavesForeignSurplusInPlace()
    {
        var strategy = StrategyWithCash((Currency.CAD, 100_000m));

        var orders = FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.USD, 20_000m)), s_fx, FxFundingPolicy.FundShortfall);

        orders.Should().BeEmpty("USD sale proceeds stay in USD under FundShortfall");
    }

    [Fact]
    public void Plan_RepatriatePolicy_ConvertsRemainingForeignSurplusToBase()
    {
        var strategy = StrategyWithCash((Currency.CAD, 100_000m), (Currency.USD, 5_000m));

        var orders = FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.USD, 20_000m)), s_fx, FxFundingPolicy.FundShortfallAndRepatriateSurplus);

        var order = orders.Should().ContainSingle().Which;
        order.SellCurrency.Should().Be(Currency.USD);
        order.BuyCurrency.Should().Be(Currency.CAD);
        order.BuyAmount.Should().Be(25_000m * 1.30m, "existing 5,000 + 20,000 proceeds, in CAD");
    }

    [Fact]
    public void Plan_TwoForeignCurrencies_RoutesThroughBaseAsTwoOrders()
    {
        // Sell 10,000 USD of one asset, buy 15,000 EUR of another; base cash is empty.
        var strategy = StrategyWithCash((Currency.CAD, 0m));

        var orders = FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.USD, 10_000m), (Currency.EUR, -15_000m)), s_fx, FxFundingPolicy.FundShortfall);

        orders.Should().HaveCount(2);
        orders[0].Should().BeEquivalentTo(new FxOrderEvent(s_signalDate, "S", Currency.CAD, Currency.EUR, 15_000m),
            "the EUR shortfall is bought with base first (22,500 CAD)");
        orders[1].Should().BeEquivalentTo(new FxOrderEvent(s_signalDate, "S", Currency.USD, Currency.CAD, 13_000m),
            "then the USD surplus (13,000 CAD) covers what it can of the base shortfall");
    }

    [Fact]
    public void Plan_MissingRateForNeededCurrency_Throws()
    {
        var strategy = StrategyWithCash((Currency.CAD, 100_000m));
        var fxWithoutUsd = new SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>>
        {
            [s_signalDate] = new SortedDictionary<Currency, decimal> { [Currency.EUR] = 1.50m },
        };

        var act = () => FxFundingPlanner.Plan(s_signalDate, strategy, Currency.CAD,
            Flows((Currency.USD, -50_000m)), fxWithoutUsd, FxFundingPolicy.FundShortfall);

        act.Should().Throw<InvalidOperationException>().WithMessage("*USD*");
    }

    // ─── SimulatedBrokerage: FX fills and ordering ───────────────────────

    [Fact]
    public async Task SubmitFxOrderAsync_RejectsNonPositiveAmountAndSameCurrency()
    {
        var broker = new SimulatedBrokerage(new ZeroCostModel(), new NoSlippage());

        var zero = () => broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.CAD, Currency.USD, 0m), CancellationToken.None);
        var same = () => broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.CAD, Currency.CAD, 10m), CancellationToken.None);

        await zero.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await same.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessPendingOrdersAsync_FxFill_UsesFillDateRateInBothDirections()
    {
        var broker = new SimulatedBrokerage(new ZeroCostModel(), new NoSlippage());
        var fills = new List<FxFillEvent>();
        broker.FxFillOccurred += (_, f) => { fills.Add(f); return Task.CompletedTask; };
        await broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.CAD, Currency.USD, 10_000m), CancellationToken.None);
        await broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.USD, Currency.CAD, 13_000m), CancellationToken.None);
        await broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.USD, Currency.EUR, 1_000m), CancellationToken.None);

        await broker.ProcessPendingOrdersAsync(s_fillDate, [], Currency.CAD, s_fx[s_fillDate], CancellationToken.None);

        fills.Should().HaveCount(3);
        fills[0].Should().BeEquivalentTo(new FxFillEvent(s_fillDate, "S", Currency.CAD, 13_000m, Currency.USD, 10_000m, 1.30m, 0m),
            "buying USD with CAD costs fx[USD] CAD per USD");
        fills[1].Should().BeEquivalentTo(new FxFillEvent(s_fillDate, "S", Currency.USD, 10_000m, Currency.CAD, 13_000m, 1m / 1.30m, 0m),
            "buying CAD with USD costs 1 / fx[USD] USD per CAD");
        fills[2].SellAmount.Should().BeApproximately(1_000m * 1.50m / 1.30m, 1e-12m,
            "a cross between two non-base currencies is priced through base: fx[EUR] / fx[USD]");
    }

    [Fact]
    public async Task ProcessPendingOrdersAsync_FxFill_ChargesTheSpreadInSellCurrency()
    {
        var broker = new SimulatedBrokerage(new ZeroCostModel(), new NoSlippage(), fxCostModel: new SpreadFxCostModel(50m));
        FxFillEvent? fill = null;
        broker.FxFillOccurred += (_, f) => { fill = f; return Task.CompletedTask; };
        await broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.CAD, Currency.USD, 10_000m), CancellationToken.None);

        await broker.ProcessPendingOrdersAsync(s_fillDate, [], Currency.CAD, s_fx[s_fillDate], CancellationToken.None);

        fill!.Commission.Should().Be(13_000m * 0.005m, "50 bps of the 13,000 CAD sold");
    }

    [Fact]
    public async Task ProcessPendingOrdersAsync_FillsAssetSellsThenFxThenAssetBuys()
    {
        var broker = new SimulatedBrokerage(new ZeroCostModel(), new NoSlippage());
        var sequence = new List<string>();
        broker.FillOccurred += (_, f) => { sequence.Add($"{f.TradeAction} {f.Symbol.Ticker}"); return Task.CompletedTask; };
        broker.FxFillOccurred += (_, f) => { sequence.Add($"FX {f.SellCurrency}->{f.BuyCurrency}"); return Task.CompletedTask; };

        // Submitted buy-first, then FX, then sell — the brokerage must not fill in submission order.
        await broker.SubmitOrderAsync(new Order(s_signalDate, "S", s_xbb, TradeAction.Buy, OrderType.Market, 10), CancellationToken.None);
        await broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.USD, Currency.CAD, 1_300m), CancellationToken.None);
        await broker.SubmitOrderAsync(new Order(s_signalDate, "S", s_vgsh, TradeAction.Sell, OrderType.Market, 10), CancellationToken.None);

        var bars = new SortedDictionary<Symbol, Bar>
        {
            [s_vgsh] = new Bar(s_fillDate, 100m, 100m, 100m, 100m, 100m, 1_000),
            [s_xbb] = new Bar(s_fillDate, 50m, 50m, 50m, 50m, 50m, 1_000),
        };
        await broker.ProcessPendingOrdersAsync(s_fillDate, bars, Currency.CAD, s_fx[s_fillDate], CancellationToken.None);

        sequence.Should().Equal("Sell VGSH", "FX USD->CAD", "Buy XBB");
    }

    [Fact]
    public async Task ProcessPendingOrdersAsync_AssetOnlyOverload_LeavesFxOrdersQueued()
    {
        var broker = new SimulatedBrokerage(new ZeroCostModel(), new NoSlippage());
        var fxFills = 0;
        broker.FxFillOccurred += (_, _) => { fxFills++; return Task.CompletedTask; };
        await broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.CAD, Currency.USD, 10m), CancellationToken.None);

        await broker.ProcessPendingOrdersAsync(s_fillDate, [], CancellationToken.None);
        fxFills.Should().Be(0, "without a base currency and an FX table the conversion cannot be priced");

        await broker.ProcessPendingOrdersAsync(s_fillDate, [], Currency.CAD, s_fx[s_fillDate], CancellationToken.None);
        fxFills.Should().Be(1, "the queued conversion fills once a table arrives");
    }

    [Fact]
    public async Task ProcessPendingOrdersAsync_FxFillWithoutRate_Throws()
    {
        var broker = new SimulatedBrokerage(new ZeroCostModel(), new NoSlippage());
        await broker.SubmitFxOrderAsync(new FxOrder(s_signalDate, "S", Currency.CAD, Currency.USD, 10m), CancellationToken.None);

        var act = () => broker.ProcessPendingOrdersAsync(s_fillDate, [], Currency.CAD, [], CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no FX rate for USD*");
    }

    // ─── FxFillEventHandler ──────────────────────────────────────────────

    [Fact]
    public async Task FxFillEventHandler_FullyFunded_MovesCashAndCreatesBuyBucket()
    {
        var strategy = StrategyWithCash((Currency.CAD, 20_000m));
        var portfolio = PortfolioFor(strategy);
        var fill = new FxFillEvent(s_fillDate, "S", Currency.CAD, 13_000m, Currency.USD, 10_000m, 1.30m, 65m);

        await new FxFillEventHandler().HandleEventAsync(portfolio, fill, CancellationToken.None);

        strategy.Cash[Currency.CAD].Should().Be(20_000m - 13_000m - 65m);
        strategy.Cash[Currency.USD].Should().Be(10_000m, "the USD bucket did not exist and is created by the fill");
    }

    [Fact]
    public async Task FxFillEventHandler_Underfunded_ClipsAllLegsProportionally()
    {
        var strategy = StrategyWithCash((Currency.CAD, 6_532.5m)); // half of 13,000 + 65
        var portfolio = PortfolioFor(strategy);
        var fill = new FxFillEvent(s_fillDate, "S", Currency.CAD, 13_000m, Currency.USD, 10_000m, 1.30m, 65m);

        await new FxFillEventHandler().HandleEventAsync(portfolio, fill, CancellationToken.None);

        strategy.Cash[Currency.CAD].Should().Be(0m, "everything the bucket held is spent");
        strategy.Cash[Currency.USD].Should().Be(5_000m, "half the requested USD, at the fill's rate and spread");
    }

    [Fact]
    public async Task FxFillEventHandler_NoSellCurrencyCash_DropsTheFill()
    {
        var strategy = StrategyWithCash((Currency.USD, 1_000m));
        var portfolio = PortfolioFor(strategy);
        var fill = new FxFillEvent(s_fillDate, "S", Currency.CAD, 13_000m, Currency.USD, 10_000m, 1.30m, 0m);

        await new FxFillEventHandler().HandleEventAsync(portfolio, fill, CancellationToken.None);

        strategy.Cash.Should().NotContainKey(Currency.CAD);
        strategy.Cash[Currency.USD].Should().Be(1_000m);
    }

    [Fact]
    public async Task FxFillEventHandler_WrongEventType_Throws()
    {
        var act = () => new FxFillEventHandler().HandleEventAsync(
            PortfolioFor(StrategyWithCash((Currency.CAD, 1m))), new SignalEvent(s_fillDate, "S", new SortedDictionary<Symbol, SignalType>()), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── FxOrderEventHandler ─────────────────────────────────────────────

    [Fact]
    public async Task FxOrderEventHandler_SubmitsThroughThePortfolio()
    {
        var portfolioMock = new Mock<IPortfolio>();
        var fxOrder = new FxOrderEvent(s_signalDate, "S", Currency.CAD, Currency.USD, 10_000m);
        portfolioMock.Setup(p => p.SubmitFxOrderAsync(fxOrder, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await new FxOrderEventHandler().HandleEventAsync(portfolioMock.Object, fxOrder, CancellationToken.None);

        portfolioMock.Verify(p => p.SubmitFxOrderAsync(fxOrder, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Portfolio_SubmitFxOrderAsync_ForwardsToBrokerAsFxOrder()
    {
        var brokerMock = new Mock<IBrokerage>();
        FxOrder? submitted = null;
        brokerMock.Setup(b => b.SubmitFxOrderAsync(It.IsAny<FxOrder>(), It.IsAny<CancellationToken>()))
            .Callback<FxOrder, CancellationToken>((o, _) => submitted = o)
            .ReturnsAsync(true);
        var strategy = StrategyWithCash((Currency.CAD, 1m));
        var portfolio = new Portfolio(
            Currency.CAD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { ["S"] = strategy }),
            new ReadOnlyDictionary<Symbol, Currency>(new Dictionary<Symbol, Currency> { [s_vgsh] = Currency.USD }),
            new ReadOnlyDictionary<Type, IEventHandler>(new Dictionary<Type, IEventHandler> { [typeof(FxFillEvent)] = new FxFillEventHandler() }),
            brokerMock.Object);

        var accepted = await portfolio.SubmitFxOrderAsync(new FxOrderEvent(s_signalDate, "S", Currency.CAD, Currency.USD, 10_000m), CancellationToken.None);

        accepted.Should().BeTrue();
        submitted.Should().BeEquivalentTo(new FxOrder(s_signalDate, "S", Currency.CAD, Currency.USD, 10_000m));
    }

    // ─── SpreadFxCostModel ───────────────────────────────────────────────

    [Fact]
    public void SpreadFxCostModel_NegativeSpread_Throws()
    {
        var act = () => new SpreadFxCostModel(-1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(25, 32.5)]
    [InlineData(100, 130)]
    public void SpreadFxCostModel_ChargesBasisPointsOfSellAmount(decimal bps, decimal expected)
    {
        new SpreadFxCostModel(bps).CalculateCommission(Currency.CAD, 13_000m, Currency.USD, 10_000m).Should().Be(expected);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static Dictionary<Currency, decimal> Flows(params (Currency Currency, decimal Amount)[] flows) =>
        flows.ToDictionary(f => f.Currency, f => f.Amount);

    private static BuyAndHoldStrategy StrategyWithCash(params (Currency Currency, decimal Amount)[] cash) =>
        new(
            "S",
            new ReadOnlyDictionary<Symbol, Currency>(new Dictionary<Symbol, Currency> { [s_vgsh] = Currency.USD }),
            new SortedDictionary<Currency, decimal>(cash.ToDictionary(c => c.Currency, c => c.Amount)),
            s_signalDate,
            new ClosePriceOrderPriceCalculationStrategy(),
            new FixedWeightPositionSizer(new ReadOnlyDictionary<Symbol, decimal>(new Dictionary<Symbol, decimal> { [s_vgsh] = 1m }), Currency.CAD));

    private static IPortfolio PortfolioFor(IStrategy strategy)
    {
        var mock = new Mock<IPortfolio>();
        mock.Setup(p => p.GetStrategy(strategy.Name)).Returns(strategy);
        return mock.Object;
    }

    private sealed class ZeroCostModel : ITransactionCostModel
    {
        public decimal CalculateCommission(decimal fillPrice, int quantity, TradeAction tradeAction) => 0m;
    }
}
