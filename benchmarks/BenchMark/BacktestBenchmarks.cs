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

namespace Boutquin.Trading.BenchMark;
/// <summary>
/// Measures the end-to-end cost of running a single-asset buy-and-hold backtest
/// (252 trading days, SPY, percentage-of-value commission at 10 bps).
/// </summary>
[MemoryDiagnoser]
public class BuyAndHoldBenchmark
{
    private static readonly Symbol s_spy = new("SPY");
    private static readonly IReadOnlyDictionary<Symbol, CurrencyCode> s_assetCurrencies =
        new Dictionary<Symbol, CurrencyCode> { { s_spy, CurrencyCode.USD } };

    private FakeBacktestDataset _dataset = null!;
    private BackTest _backtest = null!;

    [GlobalSetup]
    public void GlobalSetup() =>
        _dataset = BenchmarkHelpers.BuildDataset(
            [s_spy],
            startDate: new DateOnly(2024, 1, 2),
            tradingDays: 252,
            startPrices: [470m],
            annualVols: [0.16],
            annualDrifts: [0.10],
            seed: 42);

    [IterationSetup]
    public void IterationSetup()
    {
        const CurrencyCode usd = CurrencyCode.USD;
        var initialCash = new SortedDictionary<CurrencyCode, decimal> { { usd, 100_000m } };
        var handlers = BenchmarkHelpers.BuildHandlers();
        var sizer = new FixedWeightPositionSizer(
            new Dictionary<Symbol, decimal> { { s_spy, 1m } }, usd);
        var strategy = new BuyAndHoldStrategy(
            name: "BuyAndHold-SPY",
            assets: s_assetCurrencies,
            cash: initialCash,
            initialTimestamp: _dataset.Prices.Keys.First(),
            orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
            positionSizer: sizer);
        var benchmarkStrategy = new BuyAndHoldStrategy(
            name: "Benchmark-SPY",
            assets: s_assetCurrencies,
            cash: new SortedDictionary<CurrencyCode, decimal>(initialCash),
            initialTimestamp: _dataset.Prices.Keys.First(),
            orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
            positionSizer: sizer);
        var portfolio = BenchmarkHelpers.BuildPortfolio(
            usd, strategy, s_assetCurrencies, handlers, new SimulatedBrokerage(commissionRate: 0.001m));
        var benchmark = BenchmarkHelpers.BuildPortfolio(
            usd, benchmarkStrategy, s_assetCurrencies, handlers, new SimulatedBrokerage(commissionRate: 0.00001m));
        _backtest = new BackTest(portfolio, benchmark, usd);
    }

    [Benchmark]
    public Task<Tearsheet> Run() =>
        _backtest.RunAsync(_dataset, ignoreDataQualityIssues: true);
}

/// <summary>
/// Measures the end-to-end cost of running a three-asset minimum-variance backtest
/// (504 trading days, SPY/TLT/GLD, monthly rebalancing with LedoitWolf covariance,
/// 60-day lookback window, 10 bps commission).
/// </summary>
[MemoryDiagnoser]
public class MinimumVarianceBenchmark
{
    private static readonly Symbol s_spy = new("SPY");
    private static readonly Symbol s_tlt = new("TLT");
    private static readonly Symbol s_gld = new("GLD");

    private FakeBacktestDataset _dataset = null!;
    private BackTest _backtest = null!;

    [GlobalSetup]
    public void GlobalSetup() =>
        _dataset = BenchmarkHelpers.BuildDataset(
            [s_spy, s_tlt, s_gld],
            startDate: new DateOnly(2023, 1, 3),
            tradingDays: 504,
            startPrices: [380m, 100m, 170m],
            annualVols: [0.16, 0.10, 0.14],
            annualDrifts: [0.10, 0.04, 0.06],
            seed: 42);

    [IterationSetup]
    public void IterationSetup()
    {
        const CurrencyCode usd = CurrencyCode.USD;
        var assetCurrencies = new Dictionary<Symbol, CurrencyCode>
        {
            { s_spy, usd }, { s_tlt, usd }, { s_gld, usd },
        };
        var initialCash = new SortedDictionary<CurrencyCode, decimal> { { usd, 100_000m } };
        var handlers = BenchmarkHelpers.BuildHandlers();
        var constructionModel = new MinimumVarianceConstruction(
            new LedoitWolfConstantCorrelationEstimator());
        var positionSizer = new DynamicWeightPositionSizer(usd);
        var strategy = new ConstructionModelStrategy(
            name: "MinVar-3Asset",
            assets: assetCurrencies,
            cash: initialCash,
            orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
            positionSizer: positionSizer,
            constructionModel: constructionModel,
            rebalancingFrequency: RebalancingFrequency.Monthly,
            rebalancingTrigger: new CalendarRebalancingTrigger(),
            lookbackWindow: 60);
        var benchmarkWeights = new Dictionary<Symbol, decimal>
        {
            { s_spy, 1m / 3m }, { s_tlt, 1m / 3m }, { s_gld, 1m / 3m },
        };
        var benchmarkStrategy = new BuyAndHoldStrategy(
            name: "Benchmark-EqualWeight",
            assets: assetCurrencies,
            cash: new SortedDictionary<CurrencyCode, decimal>(initialCash),
            initialTimestamp: _dataset.Prices.Keys.First(),
            orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
            positionSizer: new FixedWeightPositionSizer(benchmarkWeights, usd));
        var portfolio = BenchmarkHelpers.BuildPortfolio(
            usd, strategy, assetCurrencies, handlers, new SimulatedBrokerage(commissionRate: 0.001m));
        var benchmark = BenchmarkHelpers.BuildPortfolio(
            usd, benchmarkStrategy, assetCurrencies, handlers, new SimulatedBrokerage(commissionRate: 0.00001m));
        _backtest = new BackTest(portfolio, benchmark, usd);
    }

    [Benchmark]
    public Task<Tearsheet> Run() =>
        _backtest.RunAsync(_dataset, ignoreDataQualityIssues: true);
}
