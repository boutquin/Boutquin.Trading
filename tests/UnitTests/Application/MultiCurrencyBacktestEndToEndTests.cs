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

using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Abstractions.Results;
using Boutquin.MarketData.Abstractions.Snapshots;
using Boutquin.Trading.Application.CostModels;
using Boutquin.Trading.Application.SlippageModels;
using Boutquin.Trading.Recipes;
using Boutquin.Trading.Recipes.Testing;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using FxRate = Boutquin.MarketData.Abstractions.Records.FxRate;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// End-to-end coverage of the engine's multi-currency path: a CAD-base portfolio holding a
/// USD-denominated asset, driven through <see cref="BacktestDatasetBuilder"/> and
/// <see cref="BackTest"/> with a fake <see cref="IDataPipeline"/> that answers FX requests per the
/// documented <see cref="FxRate"/> semantics (1 unit of the pair's base = <c>Rate</c> units of its quote).
/// </summary>
/// <remarks>
/// The asset's USD price is held constant at 100 wherever a test reads the equity curve, so every
/// base-currency move comes from FX alone and the expected curve is <c>100,000 USD × USD/CAD</c>
/// regardless of how many shares the sizer buys. That keeps each test failing for exactly one reason.
/// </remarks>
public sealed class MultiCurrencyBacktestEndToEndTests
{
    private const decimal InitialCash = 100_000m;
    private const decimal Tolerance = 1e-6m;

    private static readonly Symbol s_vgsh = new("VGSH");
    private static readonly Symbol s_xbb = new("XBB");
    private static readonly Symbol s_benchmark = new("BM");

    // Canada Day 2024: NYSE trades, the Bank of Canada publishes no FXUSDCAD observation.
    private static readonly DateOnly s_canadaDay = new(2024, 7, 1);
    private static readonly DateOnly s_priorFxPrint = new(2024, 6, 28);

    /// <summary>CAD per 1 USD — the market-convention USD/CAD quote — on eight NYSE sessions.</summary>
    private static readonly SortedDictionary<DateOnly, decimal> s_usdCad = new()
    {
        [new DateOnly(2024, 6, 24)] = 1.30m,
        [new DateOnly(2024, 6, 25)] = 1.32m,
        [new DateOnly(2024, 6, 26)] = 1.31m,
        [new DateOnly(2024, 6, 27)] = 1.34m,
        [s_priorFxPrint] = 1.33m,
        [s_canadaDay] = 1.36m,
        [new DateOnly(2024, 7, 2)] = 1.35m,
        [new DateOnly(2024, 7, 3)] = 1.37m,
    };

    private static readonly decimal[] s_benchmarkCloses = [50m, 50.5m, 50.25m, 51m, 50.75m, 51.5m, 51.25m, 52m];

    // ─── Defect 1: FX rate direction ─────────────────────────────────────

    [Fact]
    public async Task BuildAsync_ForeignCurrency_StoresBaseUnitsPerForeignUnit()
    {
        // Every consumer of the dataset's FX map multiplies a foreign-currency amount by the stored
        // rate to reach base currency (StrategyBase.ComputeTotalValue, ConstructionModelStrategy
        // .ComputeCurrentWeights). For base CAD / foreign USD that is CAD per USD (~1.3), never
        // USD per CAD (~0.77).
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad);

        var dataset = await BuildDatasetAsync(pipeline);

        using (new AssertionScope())
        {
            foreach (var (date, usdCad) in s_usdCad)
            {
                dataset.FxRates[date][Currency.USD].Should().BeApproximately(usdCad, Tolerance,
                    $"on {date} the rate stored for USD must convert a USD amount into CAD; " +
                    $"the pipeline was asked for {string.Join(", ", pipeline.RequestedPairs)}");
            }
        }
    }

    [Fact]
    public async Task RunAsync_CadBaseHoldingUsdAsset_EquityCurveFollowsUsdCad()
    {
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad);
        var dataset = await BuildDatasetAsync(pipeline);
        var (portfolio, _) = CreatePortfolio(s_vgsh, Usd(InitialCash), weight: 1m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        await backtest.RunAsync(dataset);

        using (new AssertionScope())
        {
            foreach (var (date, usdCad) in s_usdCad)
            {
                portfolio.EquityCurve[date].Should().BeApproximately(InitialCash * usdCad, Tolerance,
                    $"USD 100,000 is worth 100,000 × USD/CAD in CAD on {date}");
            }

            var curveRatio = portfolio.EquityCurve.Values.Last() / portfolio.EquityCurve.Values.First();
            curveRatio.Should().BeApproximately(1.37m / 1.30m, Tolerance,
                "USD strengthened against CAD over the run, so a USD holding gains in CAD terms");
        }
    }

    // ─── Defect 2: FX staleness — default 0, opt-in carry-forward ────────

    [Fact]
    public async Task RunAsync_FxSourceSkipsItsOwnHoliday_CarriesPriorRateForward()
    {
        // Canada Day is a Monday; the prior print is Friday 2024-06-28, 3 calendar days old.
        var fxPrintDates = s_usdCad.Keys.Where(d => d != s_canadaDay).ToList();
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad, fxPrintDates);
        var dataset = await BuildDatasetAsync(pipeline);
        var (portfolio, _) = CreatePortfolio(s_vgsh, Usd(InitialCash), weight: 1m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        var exception = await Record.ExceptionAsync(() => backtest.RunAsync(dataset, maxFxRateStalenessDays: 5));

        exception.Should().BeNull(
            "NYSE traded on Canada Day 2024 while the FX source published nothing; with an explicit bound " +
            "one missing print inside the series is bridged with the prior observation");
        portfolio.EquityCurve.Should().ContainKey(s_canadaDay);
        portfolio.EquityCurve[s_canadaDay].Should().Be(portfolio.EquityCurve[s_priorFxPrint],
            "the USD price is constant and the 2024-06-28 rate is carried forward");
    }

    [Fact]
    public async Task RunAsync_FxSourceSkipsItsOwnHoliday_DefaultAcceptsNoStaleness()
    {
        var fxPrintDates = s_usdCad.Keys.Where(d => d != s_canadaDay).ToList();
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad, fxPrintDates);
        var dataset = await BuildDatasetAsync(pipeline);
        var (portfolio, _) = CreatePortfolio(s_vgsh, Usd(InitialCash), weight: 1m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        var act = () => backtest.RunAsync(dataset);

        var assertion = await act.Should().ThrowAsync<InvalidOperationException>(
            "no staleness is accepted unless the caller opts in");
        assertion.Which.Message.Should().ContainAll("USD", "2024-07-01", "2024-06-28", "3 calendar day", "maximum of 0");
    }

    [Fact]
    public async Task RunAsync_FxGapExceedsBound_FailsNamingTheBound()
    {
        // Prints stop after 2024-06-26; the next bars are 06-27 (1 day) and 06-28 (2 days).
        var fxPrintDates = s_usdCad.Keys.Take(3).ToList();
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad, fxPrintDates);
        var dataset = await BuildDatasetAsync(pipeline);
        var (portfolio, _) = CreatePortfolio(s_vgsh, Usd(InitialCash), weight: 1m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        var act = () => backtest.RunAsync(dataset, maxFxRateStalenessDays: 1);

        var assertion = await act.Should().ThrowAsync<InvalidOperationException>();
        assertion.Which.Message.Should().ContainAll("USD", "2024-06-28", "2024-06-26", "2 calendar day", "maximum of 1");
    }

    [Fact]
    public async Task RunAsync_FxHistoryStartsAfterFirstBar_FailsLoudly()
    {
        // Carry-forward bridges gaps INSIDE the series and must never back-fill a start the FX
        // source does not cover, however generous the bound.
        var fxPrintDates = s_usdCad.Keys.Skip(2).ToList();
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad, fxPrintDates);
        var dataset = await BuildDatasetAsync(pipeline);
        var (portfolio, _) = CreatePortfolio(s_vgsh, Usd(InitialCash), weight: 1m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        var act = () => backtest.RunAsync(dataset, maxFxRateStalenessDays: 365);

        var assertion = await act.Should().ThrowAsync<InvalidOperationException>();
        assertion.Which.Message.Should().ContainAll("USD", "2024-06-24", "2024-06-26", "never back-fills");
    }

    [Fact]
    public async Task RunAsync_NegativeStalenessBound_Throws()
    {
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad);
        var dataset = await BuildDatasetAsync(pipeline);
        var (portfolio, _) = CreatePortfolio(s_vgsh, Usd(InitialCash), weight: 1m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        var act = () => backtest.RunAsync(dataset, maxFxRateStalenessDays: -1);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // ─── Defect 3: no way to fund a foreign-currency purchase ────────────

    // The end state below holds under implicit spot conversion at fill time and under an explicit
    // FX trade. If the engine instead requires funding in the asset's currency, these two tests
    // change to expect a rejection at construction, before the first bar.

    [Fact]
    public async Task RunAsync_BaseCurrencyFundedStrategy_AcquiresForeignAsset()
    {
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad);
        var dataset = await BuildDatasetAsync(pipeline);
        var (portfolio, strategy) = CreatePortfolio(s_vgsh, Cad(InitialCash), weight: 1m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        var exception = await Record.ExceptionAsync(() => backtest.RunAsync(dataset));

        using (new AssertionScope())
        {
            exception.Should().BeNull("a CAD-funded strategy must be able to buy a USD-denominated asset");
            strategy.Positions.GetValueOrDefault(s_vgsh).Should().BePositive();
        }
    }

    [Fact]
    public async Task RunAsync_BaseCurrencyFundedStrategyWithEmptyForeignBucket_AcquiresForeignAsset()
    {
        var pipeline = new SemanticFxPipeline(s_usdCad.Keys, s_usdCad);
        var dataset = await BuildDatasetAsync(pipeline);
        var cash = new SortedDictionary<Currency, decimal> { [Currency.CAD] = InitialCash, [Currency.USD] = 0m };
        var (portfolio, strategy) = CreatePortfolio(s_vgsh, cash, weight: 1m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        var exception = await Record.ExceptionAsync(() => backtest.RunAsync(dataset));

        using (new AssertionScope())
        {
            strategy.Positions.GetValueOrDefault(s_vgsh).Should().BePositive(
                "initialising an empty USD bucket must not leave CAD 100,000 permanently uninvested");
            exception.Should().BeNull();
        }
    }

    [Fact]
    public async Task RunAsync_SwitchFromUsdAssetToCadAsset_UsdProceedsFundTheCadBuyOnTheSameBar()
    {
        // Day 1: 100% VGSH (USD). Day 3: 100% XBB (CAD). No cash anywhere but the initial CAD, and a
        // constant rate so every leg is exact. On the fill bar the VGSH sale, the USD→CAD conversion
        // and the XBB buy must chain.
        var script = new Dictionary<DateOnly, Dictionary<Symbol, int>>
        {
            [new DateOnly(2024, 6, 24)] = new() { [s_vgsh] = 1000, [s_xbb] = 0 },
            [new DateOnly(2024, 6, 26)] = new() { [s_vgsh] = 0, [s_xbb] = 2600 },
        };
        var (portfolio, strategy) = CreateScriptedPortfolio(Cad(130_000m), script, FxFundingPolicy.FundShortfall);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        await backtest.RunAsync(ScriptedDataset(constantUsdCad: 1.30m));

        using (new AssertionScope())
        {
            strategy.Positions.GetValueOrDefault(s_vgsh).Should().Be(0);
            strategy.Positions.GetValueOrDefault(s_xbb).Should().Be(2600, "130,000 CAD of a 50 CAD share, funded by the 100,000 USD sale");
            strategy.Cash.GetValueOrDefault(Currency.USD).Should().Be(0m, "the whole USD proceeds were converted");
            strategy.Cash.GetValueOrDefault(Currency.CAD).Should().Be(0m);
            portfolio.EquityCurve[new DateOnly(2024, 6, 27)].Should().Be(130_000m,
                "no cost and a flat rate: the switch itself moves nothing; XBB's drift starts the next bar");
        }
    }

    [Fact]
    public async Task RunAsync_RepatriatePolicy_ConvertsSaleProceedsBackToBase()
    {
        // Buy 500 VGSH (50,000 USD, 66,000 CAD at the 06-25 fill rate), sell it two bars later.
        // The repatriation is sized at the 06-26 rate (50,000 × 1.31 = 65,500 CAD) and fills at
        // 1.34, so 48,880.60 USD buys it and the rate move leaves 1,119.40 USD behind. The policy
        // sweeps that residual on the next signal bar: 1,119.40 × 1.34 = 1,500 CAD ordered on 06-27,
        // clipped at the 06-28 rate of 1.33 to the 1,488.81 CAD the residual actually buys.
        var script = new Dictionary<DateOnly, Dictionary<Symbol, int>>
        {
            [new DateOnly(2024, 6, 24)] = new() { [s_vgsh] = 500, [s_xbb] = 0 },
            [new DateOnly(2024, 6, 26)] = new() { [s_vgsh] = 0, [s_xbb] = 0 },
        };
        var (portfolio, strategy) = CreateScriptedPortfolio(Cad(130_000m), script, FxFundingPolicy.FundShortfallAndRepatriateSurplus);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        await backtest.RunAsync(ScriptedDataset());

        using (new AssertionScope())
        {
            strategy.Positions.GetValueOrDefault(s_vgsh).Should().Be(0);
            strategy.Cash.GetValueOrDefault(Currency.USD).Should().Be(0m, "no idle USD survives a repatriating strategy");
            strategy.Cash.GetValueOrDefault(Currency.CAD).Should().BeApproximately(
                64_000m + 65_500m + (50_000m - 65_500m / 1.34m) * 1.33m, 1e-9m,
                "the main conversion at the 06-27 rate, then the residual at the 06-28 rate");
        }
    }

    [Fact]
    public async Task RunAsync_FundShortfallPolicy_LeavesSaleProceedsInTheirCurrency()
    {
        var script = new Dictionary<DateOnly, Dictionary<Symbol, int>>
        {
            [new DateOnly(2024, 6, 24)] = new() { [s_vgsh] = 500, [s_xbb] = 0 },
            [new DateOnly(2024, 6, 26)] = new() { [s_vgsh] = 0, [s_xbb] = 0 },
        };
        var (portfolio, strategy) = CreateScriptedPortfolio(Cad(130_000m), script, FxFundingPolicy.FundShortfall);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        await backtest.RunAsync(ScriptedDataset());

        using (new AssertionScope())
        {
            strategy.Cash.GetValueOrDefault(Currency.USD).Should().Be(50_000m, "proceeds stay in USD under FundShortfall");
            strategy.Cash.GetValueOrDefault(Currency.CAD).Should().Be(64_000m, "130,000 less the 66,000 the 06-25 conversion cost");
            portfolio.EquityCurve.Values.Last().Should().Be(50_000m * 1.37m + 64_000m, "USD cash is valued at the last bar's rate");
        }
    }

    [Fact]
    public async Task RunAsync_FxSpread_ReducesWhatTheConversionBuys()
    {
        // Buying 100,000 USD at the 06-25 rate of 1.32 with a 50 bps spread costs 132,660 CAD; only
        // 130,000 is held, so the fill is clipped to 97,994.87 USD and the 1,000-share buy is
        // cash-limited to 979 shares.
        var script = new Dictionary<DateOnly, Dictionary<Symbol, int>>
        {
            [new DateOnly(2024, 6, 24)] = new() { [s_vgsh] = 1000, [s_xbb] = 0 },
        };
        var (portfolio, strategy) = CreateScriptedPortfolio(Cad(130_000m), script, FxFundingPolicy.FundShortfall, fxSpreadBps: 50m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        await backtest.RunAsync(ScriptedDataset());

        using (new AssertionScope())
        {
            strategy.Positions.GetValueOrDefault(s_vgsh).Should().Be(979);
            strategy.Cash.GetValueOrDefault(Currency.CAD).Should().Be(0m);
            strategy.Cash.GetValueOrDefault(Currency.USD).Should().BeApproximately(100_000m * 130_000m / 132_660m - 97_900m, 1e-9m);
        }
    }

    // ─── Defect 4: position sizing divides a base-currency value by a native-currency price ──

    [Fact]
    public async Task RunAsync_HalfWeightInForeignAsset_SizesPositionInAssetCurrency()
    {
        // Hand-built, correctly directed FX map: isolates sizing from defect 1.
        var dataset = new FakeBacktestDataset
        {
            Prices = BuildPrices(s_usdCad.Keys),
            FxRates = new SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>>(
                s_usdCad.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new SortedDictionary<Currency, decimal> { [Currency.USD] = kvp.Value })),
        };
        var (portfolio, strategy) = CreatePortfolio(s_vgsh, Usd(InitialCash), weight: 0.5m);
        var backtest = new BackTest(portfolio, CreateBenchmark(), Currency.CAD);

        await backtest.RunAsync(dataset);

        // Signal day 2024-06-24: total = 100,000 USD × 1.30 = 130,000 CAD; half = 65,000 CAD;
        // one share = 100 USD × 1.30 = 130 CAD; 65,000 / 130 = 500 shares.
        strategy.Positions.GetValueOrDefault(s_vgsh).Should().Be(500,
            "a 50% target is 65,000 CAD of a 130 CAD share; 65,000 / 100 treats a USD price as CAD");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static SortedDictionary<Currency, decimal> Usd(decimal amount) => new() { [Currency.USD] = amount };

    private static SortedDictionary<Currency, decimal> Cad(decimal amount) => new() { [Currency.CAD] = amount };

    private static Task<IBacktestDataset> BuildDatasetAsync(IDataPipeline pipeline)
    {
        var spec = new BacktestDatasetSpec
        {
            Symbols = [s_vgsh, s_benchmark],
            StartDate = s_usdCad.Keys.First(),
            EndDate = s_usdCad.Keys.Last(),
            BaseCurrency = Currency.CAD,
            ForeignCurrencies = [Currency.USD],
        };

        return new BacktestDatasetBuilder(pipeline, NullLogger<BacktestDatasetBuilder>.Instance).BuildAsync(spec);
    }

    private static (Portfolio Portfolio, BuyAndHoldStrategy Strategy) CreatePortfolio(
        Symbol asset,
        SortedDictionary<Currency, decimal> cash,
        decimal weight)
    {
        var assets = new ReadOnlyDictionary<Symbol, Currency>(new Dictionary<Symbol, Currency> { [asset] = Currency.USD });
        var sizer = new FixedWeightPositionSizer(
            new ReadOnlyDictionary<Symbol, decimal>(new Dictionary<Symbol, decimal> { [asset] = weight }),
            Currency.CAD);
        var strategy = new BuyAndHoldStrategy(
            "Strategy", assets, cash, s_usdCad.Keys.First(), new ClosePriceOrderPriceCalculationStrategy(), sizer);
        var handlers = new Dictionary<Type, IEventHandler>
        {
            [typeof(MarketEvent)] = new MarketEventHandler(),
            [typeof(SignalEvent)] = new SignalEventHandler(),
            [typeof(OrderEvent)] = new OrderEventHandler(),
            [typeof(FillEvent)] = new FillEventHandler(),
            [typeof(FxOrderEvent)] = new FxOrderEventHandler(),
            [typeof(FxFillEvent)] = new FxFillEventHandler(),
        };
        var portfolio = new Portfolio(
            Currency.CAD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { ["Strategy"] = strategy }),
            assets,
            new ReadOnlyDictionary<Type, IEventHandler>(handlers),
            new SimulatedBrokerage(new ZeroCostModel(), new NoSlippage()));

        return (portfolio, strategy);
    }

    /// <summary>USD-funded, so the benchmark never trips the funding defect the portfolio is probing.</summary>
    private static Portfolio CreateBenchmark() => CreatePortfolio(s_benchmark, Usd(InitialCash), weight: 1m).Portfolio;

    /// <summary>
    /// A CAD-base portfolio over a USD asset (VGSH, 100 USD) and a CAD asset (XBB, 50 CAD) whose
    /// target positions are scripted per signal date, so cross-currency switches can be dictated.
    /// </summary>
    private static (Portfolio Portfolio, ScriptedStrategy Strategy) CreateScriptedPortfolio(
        SortedDictionary<Currency, decimal> cash,
        Dictionary<DateOnly, Dictionary<Symbol, int>> script,
        FxFundingPolicy policy,
        decimal fxSpreadBps = 0m)
    {
        var assets = new ReadOnlyDictionary<Symbol, Currency>(new Dictionary<Symbol, Currency>
        {
            [s_vgsh] = Currency.USD,
            [s_xbb] = Currency.CAD,
        });
        var strategy = new ScriptedStrategy("Scripted", assets, cash, new ClosePriceOrderPriceCalculationStrategy(), new ScriptedPositionSizer(script), script.Keys);
        var handlers = new Dictionary<Type, IEventHandler>
        {
            [typeof(MarketEvent)] = new MarketEventHandler(),
            [typeof(SignalEvent)] = new SignalEventHandler(policy),
            [typeof(OrderEvent)] = new OrderEventHandler(),
            [typeof(FillEvent)] = new FillEventHandler(),
            [typeof(FxOrderEvent)] = new FxOrderEventHandler(),
            [typeof(FxFillEvent)] = new FxFillEventHandler(),
        };
        var portfolio = new Portfolio(
            Currency.CAD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { ["Scripted"] = strategy }),
            assets,
            new ReadOnlyDictionary<Type, IEventHandler>(handlers),
            new SimulatedBrokerage(new ZeroCostModel(), new NoSlippage(), fxCostModel: new SpreadFxCostModel(fxSpreadBps)));

        return (portfolio, strategy);
    }

    /// <summary>
    /// Eight bars: VGSH flat at 100 USD, XBB at 50 CAD through 2024-06-27 then drifting, BM as usual.
    /// FX is the varying <see cref="s_usdCad"/> path unless a constant rate is given. Either way the
    /// equity curve has variance, which the tearsheet's Sharpe guard requires.
    /// </summary>
    private static FakeBacktestDataset ScriptedDataset(decimal? constantUsdCad = null)
    {
        var prices = BuildPrices(s_usdCad.Keys);
        decimal[] xbbCloses = [50m, 50m, 50m, 50m, 50.5m, 50.25m, 51m, 50.75m];
        var index = 0;
        foreach (var (date, day) in prices)
        {
            day[s_xbb] = FlatBar(date, xbbCloses[index++]);
        }

        return new FakeBacktestDataset
        {
            Prices = prices,
            FxRates = new SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>>(
                s_usdCad.ToDictionary(kvp => kvp.Key, kvp => new SortedDictionary<Currency, decimal> { [Currency.USD] = constantUsdCad ?? kvp.Value })),
        };
    }

    private sealed class ScriptedStrategy(
        string name,
        IReadOnlyDictionary<Symbol, Currency> assets,
        SortedDictionary<Currency, decimal> cash,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer,
        IEnumerable<DateOnly> signalDates)
        : StrategyBase(name, assets, cash, orderPriceCalculationStrategy, positionSizer)
    {
        private readonly HashSet<DateOnly> _signalDates = [.. signalDates];

        public override SignalEvent GenerateSignals(
            DateOnly timestamp,
            Currency baseCurrency,
            IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> historicalMarketData,
            IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates)
        {
            var signals = new SortedDictionary<Symbol, SignalType>();
            if (_signalDates.Contains(timestamp))
            {
                foreach (var asset in Assets.Keys)
                {
                    signals[asset] = SignalType.Rebalance;
                }
            }

            return new SignalEvent(timestamp, Name, signals);
        }
    }

    private sealed class ScriptedPositionSizer(Dictionary<DateOnly, Dictionary<Symbol, int>> script) : IPositionSizer
    {
        public IReadOnlyDictionary<Symbol, int> ComputePositionSizes(
            DateOnly timestamp,
            IReadOnlyDictionary<Symbol, SignalType> signalType,
            IStrategy strategy,
            IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> historicalMarketData,
            IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates) =>
            script.GetValueOrDefault(timestamp) ?? new Dictionary<Symbol, int>();
    }

    private static SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>> BuildPrices(IEnumerable<DateOnly> dates)
    {
        var prices = new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
        var index = 0;
        foreach (var date in dates)
        {
            prices[date] = new SortedDictionary<Symbol, Bar>
            {
                [s_vgsh] = FlatBar(date, 100m),
                [s_benchmark] = FlatBar(date, s_benchmarkCloses[index++]),
            };
        }

        return prices;
    }

    private static Bar FlatBar(DateOnly date, decimal price) => new(date, price, price, price, price, price, 1_000_000);

    private sealed class ZeroCostModel : ITransactionCostModel
    {
        public decimal CalculateCommission(decimal fillPrice, int quantity, TradeAction tradeAction) => 0m;
    }

    /// <summary>
    /// Answers price requests from <see cref="BuildPrices"/> and FX requests from one USD/CAD path,
    /// honouring the requested pair's orientation: <c>FxPair(USD, CAD)</c> yields CAD per USD,
    /// <c>FxPair(CAD, USD)</c> yields its reciprocal.
    /// </summary>
    private sealed class SemanticFxPipeline : IDataPipeline
    {
        private readonly SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>> _prices;
        private readonly IReadOnlyDictionary<DateOnly, decimal> _usdCad;
        private readonly IReadOnlyCollection<DateOnly> _fxPrintDates;

        public SemanticFxPipeline(
            IEnumerable<DateOnly> tradingDates,
            IReadOnlyDictionary<DateOnly, decimal> usdCad,
            IReadOnlyCollection<DateOnly>? fxPrintDates = null)
        {
            _prices = BuildPrices(tradingDates);
            _usdCad = usdCad;
            _fxPrintDates = fxPrintDates ?? [.. usdCad.Keys];
        }

        public List<FxPair> RequestedPairs { get; } = [];

        public Task<DataEnvelope<IReadOnlyList<TRecord>>> ExecuteAsync<TRequest, TRecord>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IDataRequest
        {
            object payload = request switch
            {
                PriceHistoryRequest prices => Bars(prices.Symbols.Single()),
                FxHistoryRequest fx => Rates(fx.Pairs.Single()),
                _ => throw new NotSupportedException($"Unexpected request {request.GetType().Name}."),
            };

            var records = (IReadOnlyList<TRecord>)payload;
            return Task.FromResult(new DataEnvelope<IReadOnlyList<TRecord>>(
                records, new DataCoverage(records.Count, records.Count, 0, 1.0m), [], []));
        }

        public Task<IReadOnlyDictionary<TDatasetKey, DataEnvelope<IReadOnlyList<TRecord>>>>
            ExecutePartitionedAsync<TRequest, TRecord, TPartitionKey, TDatasetKey>(
                TRequest request,
                Func<TRequest, TPartitionKey, TRequest> splitForPartition,
                Func<TRequest, TPartitionKey, TDatasetKey> buildDatasetKey,
                CancellationToken cancellationToken = default)
            where TRequest : IBatchDataRequest<TPartitionKey>
            where TDatasetKey : notnull =>
            throw new NotSupportedException();

        public Task<IDataSnapshot> AcquireReadSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private List<Bar> Bars(Symbol symbol) =>
            [.. _prices.Values.Where(day => day.ContainsKey(symbol)).Select(day => day[symbol])];

        private List<FxRate> Rates(FxPair pair)
        {
            RequestedPairs.Add(pair);

            Func<decimal, decimal> orient = (pair.BaseCurrency, pair.QuoteCurrency) switch
            {
                (Currency.USD, Currency.CAD) => usdCad => usdCad,
                (Currency.CAD, Currency.USD) => usdCad => 1m / usdCad,
                _ => throw new NotSupportedException($"No fixture for {pair}."),
            };

            return [.. _fxPrintDates.Order().Select(date => new FxRate(date, orient(_usdCad[date])))];
        }
    }
}
