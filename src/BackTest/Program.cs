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

using System.Collections.ObjectModel;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Adapter.BankOfCanada;
using Boutquin.MarketData.Adapter.FamaFrench;
using Boutquin.MarketData.Adapter.Frankfurter;
using Boutquin.MarketData.Adapter.Fred;
using Boutquin.MarketData.Adapter.Tiingo;
using Boutquin.MarketData.Adapter.TwelveData;
using Boutquin.MarketData.DependencyInjection;
using Boutquin.Trading.Application;
using Boutquin.Trading.Application.Brokers;
using Boutquin.Trading.Application.CostModels;
using Boutquin.Trading.Application.EventHandlers;
using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.SlippageModels;
using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.Events;
using Boutquin.Trading.Domain.Interfaces;
using Boutquin.Trading.Recipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── Environment variables ────────────────────────────────────────────────
var tiingoToken = Environment.GetEnvironmentVariable("TIINGO_API_KEY")
    ?? throw new InvalidOperationException("TIINGO_API_KEY is not set.");
var fredApiKey = Environment.GetEnvironmentVariable("FRED_API_KEY")
    ?? throw new InvalidOperationException("FRED_API_KEY is not set.");
var twelveDataApiKey = Environment.GetEnvironmentVariable("TWELVEDATA_API_KEY")
    ?? throw new InvalidOperationException("TWELVEDATA_API_KEY is not set.");

// ── DI host ──────────────────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

// MarketData kernel (transport, L1/L2 cache, snapshot store)
builder.Services.AddMarketDataKernel();

// Adapters
builder.Services.AddMarketDataTiingo(opts => opts.ApiToken = tiingoToken);
builder.Services.AddMarketDataFrankfurter();
builder.Services.AddMarketDataFred(opts => opts.ApiKey = fredApiKey);
builder.Services.AddMarketDataFamaFrench();
builder.Services.AddMarketDataTwelveData(opts => opts.ApiKey = twelveDataApiKey);
builder.Services.AddMarketDataBankOfCanada();

// Recipe layer
builder.Services.AddSingleton<BacktestDatasetBuilder>();

using var host = builder.Build();
var sp = host.Services;
var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("BackTest");

// ── Backtest configuration ───────────────────────────────────────────────
const CurrencyCode baseCurrency = CurrencyCode.USD;
var startDate = new DateOnly(2020, 1, 2);
var endDate = new DateOnly(2024, 12, 31);
var initialCash = 100_000m;

// US equity universe
var spy = new Symbol("SPY");
var tlt = new Symbol("TLT");
var gld = new Symbol("GLD");
Symbol[] symbols = [spy, tlt, gld];

var assetWeights = new Dictionary<Symbol, decimal>
{
    [spy] = 0.60m,
    [tlt] = 0.30m,
    [gld] = 0.10m,
};
var assetCurrencies = symbols.ToDictionary(a => a, _ => baseCurrency);
var positionSizer = new FixedWeightPositionSizer(assetWeights, baseCurrency);
var costModel = new PercentageOfValueCostModel(0.001m); // 10 bps
var slippage = new NoSlippage();

// ── Portfolio setup ──────────────────────────────────────────────────────
var strategy = new BuyAndHoldStrategy(
    "US-60-30-10",
    new SortedDictionary<Symbol, CurrencyCode>(assetCurrencies),
    new SortedDictionary<CurrencyCode, decimal> { { baseCurrency, initialCash } },
    startDate,
    new ClosePriceOrderPriceCalculationStrategy(),
    positionSizer);

var broker = new SimulatedBrokerage(costModel, slippage);
var handlers = new Dictionary<Type, IEventHandler>
{
    { typeof(OrderEvent), new OrderEventHandler() },
    { typeof(MarketEvent), new MarketEventHandler() },
    { typeof(FillEvent), new FillEventHandler() },
    { typeof(SignalEvent), new SignalEventHandler() },
};

var portfolio = new Portfolio(
    baseCurrency,
    new ReadOnlyDictionary<string, IStrategy>(
        new Dictionary<string, IStrategy> { { strategy.Name, strategy } }),
    new SortedDictionary<Symbol, CurrencyCode>(assetCurrencies),
    handlers,
    broker);

// Benchmark: buy-and-hold SPY only
var benchWeights = new Dictionary<Symbol, decimal> { [spy] = 1.0m };
var benchSizer = new FixedWeightPositionSizer(benchWeights, baseCurrency);
var benchStrategy = new BuyAndHoldStrategy(
    "SPY-Benchmark",
    new SortedDictionary<Symbol, CurrencyCode> { { spy, baseCurrency } },
    new SortedDictionary<CurrencyCode, decimal> { { baseCurrency, initialCash } },
    startDate,
    new ClosePriceOrderPriceCalculationStrategy(),
    benchSizer);

var benchBroker = new SimulatedBrokerage(costModel, slippage);
var benchHandlers = new Dictionary<Type, IEventHandler>
{
    { typeof(OrderEvent), new OrderEventHandler() },
    { typeof(MarketEvent), new MarketEventHandler() },
    { typeof(FillEvent), new FillEventHandler() },
    { typeof(SignalEvent), new SignalEventHandler() },
};

var benchmarkPortfolio = new Portfolio(
    baseCurrency,
    new ReadOnlyDictionary<string, IStrategy>(
        new Dictionary<string, IStrategy> { { benchStrategy.Name, benchStrategy } }),
    new SortedDictionary<Symbol, CurrencyCode> { { spy, baseCurrency } },
    benchHandlers,
    benchBroker);

// ── Fetch data via kernel pipeline ───────────────────────────────────────
logger.LogInformation("Fetching market data via kernel pipeline...");

var datasetBuilder = sp.GetRequiredService<BacktestDatasetBuilder>();
var spec = new BacktestDatasetSpec
{
    Symbols = symbols,
    StartDate = startDate,
    EndDate = endDate,
    BaseCurrency = baseCurrency,
    ForeignCurrencies = [],
};

var dataset = await datasetBuilder.BuildAsync(spec).ConfigureAwait(false);

logger.LogInformation(
    "Dataset ready: {PriceDays} price days, {Issues} issues, {Provenance} provenance records",
    dataset.Prices.Count, dataset.Issues.Count, dataset.Provenance.Count);

// ── Run backtest ─────────────────────────────────────────────────────────
logger.LogInformation("Running backtest: {Start} to {End}", startDate, endDate);

var backtest = new BackTest(portfolio, benchmarkPortfolio, baseCurrency,
    sp.GetRequiredService<ILoggerFactory>().CreateLogger<BackTest>());
var tearsheet = await backtest.RunAsync(dataset).ConfigureAwait(false);

// ── Print results ────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════");
Console.WriteLine("  BACKTEST RESULTS: US 60/30/10 (SPY/TLT/GLD)");
Console.WriteLine("═══════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine(FormattableString.Invariant($"  Annualized Return:    {tearsheet.AnnualizedReturn,10:P2}"));
Console.WriteLine(FormattableString.Invariant($"  CAGR:                 {tearsheet.CAGR,10:P2}"));
Console.WriteLine(FormattableString.Invariant($"  Volatility:           {tearsheet.Volatility,10:P2}"));
Console.WriteLine(FormattableString.Invariant($"  Sharpe Ratio:         {tearsheet.SharpeRatio,10:F3}"));
Console.WriteLine(FormattableString.Invariant($"  Sortino Ratio:        {tearsheet.SortinoRatio,10:F3}"));
Console.WriteLine(FormattableString.Invariant($"  Max Drawdown:         {tearsheet.MaxDrawdown,10:P2}"));
Console.WriteLine(FormattableString.Invariant($"  Max DD Duration:      {tearsheet.MaxDrawdownDuration,10} days"));
Console.WriteLine(FormattableString.Invariant($"  Calmar Ratio:         {tearsheet.CalmarRatio,10:F3}"));
Console.WriteLine(FormattableString.Invariant($"  Omega Ratio:          {tearsheet.OmegaRatio,10:F3}"));
Console.WriteLine();
Console.WriteLine(FormattableString.Invariant($"  Alpha:                {tearsheet.Alpha,10:F4}"));
Console.WriteLine(FormattableString.Invariant($"  Beta:                 {tearsheet.Beta,10:F3}"));
Console.WriteLine(FormattableString.Invariant($"  Information Ratio:    {tearsheet.InformationRatio,10:F3}"));
Console.WriteLine();
Console.WriteLine(FormattableString.Invariant($"  VaR (95%):            {tearsheet.HistoricalVaR,10:P2}"));
Console.WriteLine(FormattableString.Invariant($"  CVaR (95%):           {tearsheet.ConditionalVaR,10:P2}"));
Console.WriteLine(FormattableString.Invariant($"  Skewness:             {tearsheet.Skewness,10:F3}"));
Console.WriteLine(FormattableString.Invariant($"  Kurtosis:             {tearsheet.Kurtosis,10:F3}"));
Console.WriteLine(FormattableString.Invariant($"  Win Rate:             {tearsheet.WinRate,10:P2}"));
Console.WriteLine(FormattableString.Invariant($"  Profit Factor:        {tearsheet.ProfitFactor,10:F3}"));
Console.WriteLine(FormattableString.Invariant($"  Recovery Factor:      {tearsheet.RecoveryFactor,10:F3}"));
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════");
