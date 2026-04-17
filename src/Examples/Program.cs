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

Console.WriteLine("Boutquin.Trading Examples");
Console.WriteLine(new string('─', 50));

var registry = BuildExampleRegistry();

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp(registry);
    return;
}

string? selectedId = ParseExampleArg(args, out string? parseError);
if (parseError is not null)
{
    Console.Error.WriteLine($"Error: {parseError}");
    Console.Error.WriteLine("Use --help to see available examples.");
    return;
}

PrintListing(registry, selectedId);
Console.WriteLine();

if (selectedId is not null)
{
    if (!registry.TryGetValue(selectedId, out var entry))
    {
        Console.Error.WriteLine($"Example '{selectedId}' not found. Use --help to see available examples.");
        return;
    }
    await entry.Run().ConfigureAwait(false);
}
else
{
    foreach (var entry in registry.Values)
    {
        await entry.Run().ConfigureAwait(false);
    }
}

// ── Registry ──────────────────────────────────────────────────────────────────

static SortedDictionary<string, ExampleEntry> BuildExampleRegistry()
{
    static ExampleEntry Sync(string id, string desc, Action run) =>
        new(id, desc, () => { run(); return Task.CompletedTask; });
    static ExampleEntry Async(string id, string desc, Func<Task> run) =>
        new(id, desc, run);

    return new SortedDictionary<string, ExampleEntry>(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = Async("01", "Buy-and-hold backtest (SPY, 1 year)", Example01BuyAndHold),
        ["02"] = Async("02", "Minimum variance backtest (SPY/TLT/GLD, 2 years)", Example02RiskParity),
        ["03"] = Sync("03", "Cost and slippage model comparison", Example03CostAndSlippage),
        ["04"] = Sync("04", "Covariance estimator comparison", Example04CovarianceEstimators),
        ["05"] = Async("05", "Post-backtest analytics and attribution", Example05Analytics),
    };
}

static string? ParseExampleArg(string[] args, out string? error)
{
    error = null;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--example" && i + 1 < args.Length)
        {
            return NormalizeId(args[i + 1]);
        }

        if (args[i].StartsWith("--example=", StringComparison.Ordinal))
        {
            return NormalizeId(args[i]["--example=".Length..]);
        }
    }
    return null;
}

static string NormalizeId(string raw)
{
    if (int.TryParse(raw, out int n))
    {
        return n.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
    }

    return raw;
}

static void PrintListing(SortedDictionary<string, ExampleEntry> registry, string? selectedId)
{
    Console.WriteLine("Usage: dotnet run -- [--example <id>] [--help]");
    Console.WriteLine();
    Console.WriteLine("Available examples:");
    foreach (var (id, entry) in registry)
    {
        string marker = string.Equals(id, selectedId, StringComparison.OrdinalIgnoreCase) ? "▶" : " ";
        Console.WriteLine($"  {marker} --example {entry.Id}  {entry.Description}");
    }
}

static void PrintHelp(SortedDictionary<string, ExampleEntry> registry) =>
    PrintListing(registry, selectedId: null);

// ── Example 01 — Buy and Hold ─────────────────────────────────────────────────

static async Task Example01BuyAndHold()
{
    Console.WriteLine("── Example 01: Buy-and-hold backtest (SPY, 1 year) ──");

    const CurrencyCode baseCurrency = CurrencyCode.USD;
    var spy = new Symbol("SPY");
    Symbol[] assets = [spy];
    var assetCurrencies = new Dictionary<Symbol, CurrencyCode> { { spy, CurrencyCode.USD } };

    // 1 year of synthetic SPY prices (annualised vol ~16%, drift ~10%)
    var dataset = BuildSyntheticDataset(
        assets,
        startDate: new DateOnly(2024, 1, 2),
        tradingDays: 252,
        startPrices: [470m],
        annualVols: [0.16],
        annualDrifts: [0.10],
        seed: 42);

    var initialCash = new SortedDictionary<CurrencyCode, decimal> { { baseCurrency, 100_000m } };
    var handlers = BuildHandlers();
    var broker = new SimulatedBrokerage(commissionRate: 0.001m);

    var positionSizer = new FixedWeightPositionSizer(
        new Dictionary<Symbol, decimal> { { spy, 1m } }, baseCurrency);

    var strategy = new BuyAndHoldStrategy(
        name: "BuyAndHold-SPY",
        assets: assetCurrencies,
        cash: initialCash,
        initialTimestamp: dataset.Prices.Keys.First(),
        orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
        positionSizer: positionSizer);

    var benchmarkStrategy = new BuyAndHoldStrategy(
        name: "Benchmark-SPY",
        assets: assetCurrencies,
        cash: new SortedDictionary<CurrencyCode, decimal>(initialCash),
        initialTimestamp: dataset.Prices.Keys.First(),
        orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
        positionSizer: positionSizer);

    var portfolio = BuildPortfolio(baseCurrency, strategy, assetCurrencies, handlers, broker);
    var benchmark = BuildPortfolio(baseCurrency, benchmarkStrategy, assetCurrencies, handlers,
        new SimulatedBrokerage(commissionRate: 0.00001m));

    var backtest = new BackTest(portfolio, benchmark, baseCurrency);
    Tearsheet ts = await backtest.RunAsync(dataset, ignoreDataQualityIssues: true).ConfigureAwait(false);

    PrintTearsheet(ts);
    Console.WriteLine();
}

// ── Example 02 — Minimum Variance ────────────────────────────────────────────

static async Task Example02RiskParity()
{
    Console.WriteLine("── Example 02: Minimum variance backtest (SPY/TLT/GLD, 2 years) ──");

    const CurrencyCode baseCurrency = CurrencyCode.USD;
    var spy = new Symbol("SPY");
    var tlt = new Symbol("TLT");
    var gld = new Symbol("GLD");
    Symbol[] assets = [spy, tlt, gld];
    var assetCurrencies = new Dictionary<Symbol, CurrencyCode>
    {
        { spy, CurrencyCode.USD },
        { tlt, CurrencyCode.USD },
        { gld, CurrencyCode.USD },
    };

    var dataset = BuildSyntheticDataset(
        assets,
        startDate: new DateOnly(2023, 1, 3),
        tradingDays: 504,
        startPrices: [380m, 100m, 170m],
        annualVols: [0.16, 0.10, 0.14],
        annualDrifts: [0.10, 0.04, 0.06],
        seed: 42);

    var initialCash = new SortedDictionary<CurrencyCode, decimal> { { baseCurrency, 100_000m } };
    var handlers = BuildHandlers();
    var broker = new SimulatedBrokerage(commissionRate: 0.001m);

    var constructionModel = new MinimumVarianceConstruction(new LedoitWolfConstantCorrelationEstimator());
    var positionSizer = new DynamicWeightPositionSizer(baseCurrency);

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
        { spy, 1m / 3m }, { tlt, 1m / 3m }, { gld, 1m / 3m },
    };
    var benchmarkSizer = new FixedWeightPositionSizer(benchmarkWeights, baseCurrency);
    var benchmarkStrategy = new BuyAndHoldStrategy(
        name: "Benchmark-EqualWeight",
        assets: assetCurrencies,
        cash: new SortedDictionary<CurrencyCode, decimal>(initialCash),
        initialTimestamp: dataset.Prices.Keys.First(),
        orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
        positionSizer: benchmarkSizer);

    var portfolio = BuildPortfolio(baseCurrency, strategy, assetCurrencies, handlers, broker);
    var benchmark = BuildPortfolio(baseCurrency, benchmarkStrategy, assetCurrencies, handlers,
        new SimulatedBrokerage(commissionRate: 0.00001m));

    var backtest = new BackTest(portfolio, benchmark, baseCurrency);
    Tearsheet ts = await backtest.RunAsync(dataset, ignoreDataQualityIssues: true).ConfigureAwait(false);

    PrintTearsheet(ts);
    Console.WriteLine();
}

// ── Example 03 — Cost and Slippage Models ────────────────────────────────────

static void Example03CostAndSlippage()
{
    Console.WriteLine("── Example 03: Cost and slippage model comparison ──");

    const decimal price = 450m;
    const int quantity = 100;
    const TradeAction action = TradeAction.Buy;

    ITransactionCostModel[] costModels =
    [
        new FixedPerTradeCostModel(fixedAmount: 1.00m),
        new PerShareCostModel(perShareRate: 0.005m),
        new PercentageOfValueCostModel(rate: 0.001m),
        new TieredCostModel(
        [
            (10_000m,        0.002m),
            (100_000m,       0.001m),
            (decimal.MaxValue, 0.0005m),
        ]),
    ];

    Console.WriteLine($"  Trade: {quantity} shares @ {price:C}  (value {price * quantity:C})");
    Console.WriteLine("  Commission by model:");
    foreach (var model in costModels)
    {
        decimal commission = model.CalculateCommission(price, quantity, action);
        Console.WriteLine($"    {model.GetType().Name,-35}  {commission:F4}");
    }

    Console.WriteLine();
    Console.WriteLine("  Fill price (slippage) by model:");

    ISlippageModel[] slippageModels =
    [
        new NoSlippage(),
        new FixedSlippage(fixedAmount: 0.01m),
        new PercentageSlippage(percentage: 0.001m),
        new SpreadSlippage(halfSpreads: new Dictionary<Symbol, decimal>(), defaultHalfSpread: 0.05m),
    ];

    foreach (var model in slippageModels)
    {
        decimal filled = model.CalculateFillPrice(price, quantity, action);
        Console.WriteLine($"    {model.GetType().Name,-35}  {filled:F4}  (impact {filled - price:+0.0000;-0.0000})");
    }

    Console.WriteLine();
}

// ── Example 04 — Covariance Estimator Comparison ─────────────────────────────

static void Example04CovarianceEstimators()
{
    Console.WriteLine("── Example 04: Covariance estimator comparison ──");

    var rng = new Random(42);
    const int n = 3, t = 120;
    double[] vols = [0.16, 0.10, 0.14];
    string[] names = ["SPY", "TLT", "GLD"];

    decimal[][] returns = Enumerable.Range(0, n)
        .Select(i => Enumerable.Range(0, t)
            .Select(_ => (decimal)(rng.NextGaussian() * vols[i] / Math.Sqrt(252)))
            .ToArray())
        .ToArray();

    (string Name, ICovarianceEstimator Estimator)[] estimators =
    [
        ("Sample",                     new SampleCovarianceEstimator()),
        ("EWMA (λ=0.94)",              new ExponentiallyWeightedCovarianceEstimator(lambda: 0.94m)),
        ("LedoitWolf-ConstantCorr",    new LedoitWolfConstantCorrelationEstimator()),
        ("QuadraticInverseShrinkage",  new QuadraticInverseShrinkageEstimator()),
        ("Denoised (RMT)",             new DenoisedCovarianceEstimator()),
    ];

    Console.WriteLine($"  Assets : {string.Join(", ", names)}  |  T={t} observations");
    Console.WriteLine();

    foreach (var (name, estimator) in estimators)
    {
        decimal[,] cov = estimator.Estimate(returns);
        Console.Write($"  {name,-30}  variances: ");
        for (int i = 0; i < n; i++)
        {
            Console.Write($"{names[i]}={cov[i, i]:F6}  ");
        }

        Console.WriteLine();
    }

    Console.WriteLine();
}

// ── Example 05 — Post-Backtest Analytics and Attribution ─────────────────────

static async Task Example05Analytics()
{
    Console.WriteLine("── Example 05: Post-backtest analytics and attribution ──");

    // Run a quick 2-asset backtest to get real equity curves and returns
    const CurrencyCode baseCurrency = CurrencyCode.USD;
    var spy = new Symbol("SPY");
    var tlt = new Symbol("TLT");
    Symbol[] assets = [spy, tlt];
    var assetCurrencies = new Dictionary<Symbol, CurrencyCode>
    {
        { spy, CurrencyCode.USD },
        { tlt, CurrencyCode.USD },
    };

    var dataset = BuildSyntheticDataset(
        assets,
        startDate: new DateOnly(2024, 1, 2),
        tradingDays: 252,
        startPrices: [470m, 100m],
        annualVols: [0.16, 0.10],
        annualDrifts: [0.10, 0.04],
        seed: 99);

    var initialCash = new SortedDictionary<CurrencyCode, decimal> { { baseCurrency, 100_000m } };
    var handlers = BuildHandlers();

    var positionSizer60_40 = new FixedWeightPositionSizer(
        new Dictionary<Symbol, decimal> { { spy, 0.6m }, { tlt, 0.4m } }, baseCurrency);

    var strategy = new BuyAndHoldStrategy(
        name: "Portfolio-60-40",
        assets: assetCurrencies,
        cash: initialCash,
        initialTimestamp: dataset.Prices.Keys.First(),
        orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
        positionSizer: positionSizer60_40);

    var benchmarkAssetCurrencies = new ReadOnlyDictionary<Symbol, CurrencyCode>(
        new Dictionary<Symbol, CurrencyCode> { { spy, CurrencyCode.USD } });
    var benchmarkSizer = new FixedWeightPositionSizer(
        new Dictionary<Symbol, decimal> { { spy, 1m } }, baseCurrency);
    var benchmarkStrategy = new BuyAndHoldStrategy(
        name: "Benchmark-SPY",
        assets: benchmarkAssetCurrencies,
        cash: new SortedDictionary<CurrencyCode, decimal>(initialCash),
        initialTimestamp: dataset.Prices.Keys.First(),
        orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
        positionSizer: benchmarkSizer);

    var portfolio = BuildPortfolio(baseCurrency, strategy, assetCurrencies, handlers,
        new SimulatedBrokerage(commissionRate: 0.001m));
    var benchmark = BuildPortfolio(baseCurrency, benchmarkStrategy, benchmarkAssetCurrencies, handlers,
        new SimulatedBrokerage(commissionRate: 0.00001m));

    var backtest = new BackTest(portfolio, benchmark, baseCurrency);
    Tearsheet ts = await backtest.RunAsync(dataset, ignoreDataQualityIssues: true).ConfigureAwait(false);

    // Brinson-Fachler attribution (single period — full run)
    Console.WriteLine("  Brinson-Fachler attribution (60/40 SPY-TLT vs SPY-only benchmark):");
    var pWeights = new Dictionary<Symbol, decimal> { { spy, 0.6m }, { tlt, 0.4m } };
    var bWeights = new Dictionary<Symbol, decimal> { { spy, 1.0m }, { tlt, 0.0m } };
    var pReturns = ComputeAssetReturns(dataset, assets);
    var bReturns = new Dictionary<Symbol, decimal> { { spy, pReturns[spy] }, { tlt, pReturns[tlt] } };

    BrinsonFachlerResult bf = BrinsonFachlerAttributor.Attribute(
        assets, pWeights, bWeights, pReturns, bReturns);
    Console.WriteLine($"    Allocation effect  : {bf.AllocationEffect:P2}");
    Console.WriteLine($"    Selection effect   : {bf.SelectionEffect:P2}");
    Console.WriteLine($"    Interaction effect : {bf.InteractionEffect:P2}");
    Console.WriteLine($"    Total active return: {bf.TotalActiveReturn:P2}");
    Console.WriteLine();

    // Factor regression on portfolio daily returns
    Console.WriteLine("  Factor regression (portfolio returns ~ SPY market factor):");
    decimal[] portReturns = ComputeDailyReturns(ts.EquityCurve);
    decimal[] mktReturns = ComputeAssetDailyReturns(dataset, spy);
    if (portReturns.Length >= 4)
    {
        int len = Math.Min(portReturns.Length, mktReturns.Length);
        FactorRegressionResult reg = FactorRegressor.Regress(
            portfolioReturns: portReturns[..len],
            factorNames: ["Mkt-RF"],
            factorReturns: [mktReturns[..len]]);
        Console.WriteLine($"    Alpha (annualised) : {reg.Alpha * 252:P2}");
        Console.WriteLine($"    Market beta        : {reg.FactorLoadings["Mkt-RF"]:F3}");
        Console.WriteLine($"    R-squared          : {reg.RSquared:P1}");
    }
    Console.WriteLine();

    // Correlation analysis
    Console.WriteLine("  Correlation analysis (SPY / TLT):");
    decimal[][] assetReturns = assets.Select(a => ComputeAssetDailyReturns(dataset, a)).ToArray();
    decimal[] weights = [0.6m, 0.4m];
    int minLen = assetReturns.Min(r => r.Length);
    decimal[][] trimmed = assetReturns.Select(r => r[..minLen]).ToArray();

    CorrelationAnalysisResult corr = CorrelationAnalyzer.Analyze(assets, trimmed, weights);
    Console.WriteLine($"    Diversification ratio     : {corr.DiversificationRatio:F3}");
    Console.WriteLine($"    Corr(SPY, TLT)            : {corr.CorrelationMatrix[0, 1]:F3}");
    Console.WriteLine();

    // Drawdown analysis on portfolio equity curve
    Console.WriteLine("  Drawdown analysis (top 3 drawdowns):");
    IReadOnlyList<DrawdownPeriod> drawdowns = DrawdownAnalyzer.AnalyzeDrawdownPeriods(ts.EquityCurve);
    foreach (var dd in drawdowns.OrderByDescending(d => d.Depth).Take(3))
    {
        string recovery = dd.RecoveryDate.HasValue ? dd.RecoveryDate.Value.ToString() : "ongoing";
        Console.WriteLine($"    {dd.StartDate}  depth {dd.Depth:P2}  duration {dd.DurationDays}d  recovery {recovery}");
    }
    if (drawdowns.Count == 0)
    {
        Console.WriteLine("    No drawdowns detected.");
    }

    Console.WriteLine();
}

// ── Shared helpers ────────────────────────────────────────────────────────────

static FakeBacktestDataset BuildSyntheticDataset(
    IReadOnlyList<Symbol> assets,
    DateOnly startDate,
    int tradingDays,
    decimal[] startPrices,
    double[] annualVols,
    double[]? annualDrifts = null,
    int seed = 42)
{
    var rng = new Random(seed);
    var prices = new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
    decimal[] current = startPrices.ToArray();
    var date = startDate;

    for (int day = 0; day < tradingDays; day++)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        var dayBars = new SortedDictionary<Symbol, Bar>();
        for (int i = 0; i < assets.Count; i++)
        {
            double drift = (annualDrifts?[i] ?? 0.0) / 252.0;
            double sigma = annualVols[i] / Math.Sqrt(252);
            decimal ret = (decimal)(drift + sigma * rng.NextGaussian());
            decimal close = Math.Max(0.01m, current[i] * (1m + ret));
            decimal open = Math.Max(0.01m, current[i] * (1m + (decimal)(sigma * rng.NextGaussian() * 0.3)));
            decimal high = Math.Max(open, close) * (1m + Math.Abs((decimal)(sigma * rng.NextGaussian() * 0.2)));
            decimal low = Math.Min(open, close) * Math.Max(0.001m, 1m - Math.Abs((decimal)(sigma * rng.NextGaussian() * 0.2)));

            dayBars[assets[i]] = new Bar(date, open, high, low, close, close, 5_000_000L);
            current[i] = close;
        }

        prices[date] = dayBars;
        date = date.AddDays(1);
    }

    return new FakeBacktestDataset { Prices = prices };
}

static IReadOnlyDictionary<Type, IEventHandler> BuildHandlers() =>
    new Dictionary<Type, IEventHandler>
    {
        { typeof(OrderEvent),  new OrderEventHandler() },
        { typeof(MarketEvent), new MarketEventHandler() },
        { typeof(FillEvent),   new FillEventHandler() },
        { typeof(SignalEvent), new SignalEventHandler() },
    };

static Portfolio BuildPortfolio(
    CurrencyCode baseCurrency,
    IStrategy strategy,
    IReadOnlyDictionary<Symbol, CurrencyCode> assetCurrencies,
    IReadOnlyDictionary<Type, IEventHandler> handlers,
    IBrokerage broker) =>
    new(baseCurrency,
        new ReadOnlyDictionary<string, IStrategy>(
            new Dictionary<string, IStrategy> { { strategy.Name, strategy } }),
        assetCurrencies,
        handlers,
        broker);

static void PrintTearsheet(Tearsheet ts)
{
    Console.WriteLine($"  Annualised return : {ts.AnnualizedReturn:P2}");
    Console.WriteLine($"  CAGR              : {ts.CAGR:P2}");
    Console.WriteLine($"  Sharpe ratio      : {ts.SharpeRatio:F2}");
    Console.WriteLine($"  Sortino ratio     : {ts.SortinoRatio:F2}");
    Console.WriteLine($"  Max drawdown      : {ts.MaxDrawdown:P2}");
    Console.WriteLine($"  Max DD duration   : {ts.MaxDrawdownDuration} trading days");
    Console.WriteLine($"  Calmar ratio      : {ts.CalmarRatio:F2}");
    Console.WriteLine($"  Volatility        : {ts.Volatility:P2}");
    Console.WriteLine($"  Beta vs benchmark : {ts.Beta:F3}");
    Console.WriteLine($"  Alpha vs benchmark: {ts.Alpha:P2}");
}

static decimal[] ComputeDailyReturns(SortedDictionary<DateOnly, decimal> equityCurve)
{
    decimal[] values = [.. equityCurve.Values];
    if (values.Length < 2)
    {
        return [];
    }

    return Enumerable.Range(1, values.Length - 1)
        .Select(i => values[i - 1] == 0m ? 0m : (values[i] - values[i - 1]) / values[i - 1])
        .ToArray();
}

static decimal[] ComputeAssetDailyReturns(FakeBacktestDataset dataset, Symbol asset)
{
    decimal[] closes = dataset.Prices.Values
        .Where(d => d.ContainsKey(asset))
        .Select(d => d[asset].Close)
        .ToArray();
    if (closes.Length < 2)
    {
        return [];
    }

    return Enumerable.Range(1, closes.Length - 1)
        .Select(i => closes[i - 1] == 0m ? 0m : (closes[i] - closes[i - 1]) / closes[i - 1])
        .ToArray();
}

static IReadOnlyDictionary<Symbol, decimal> ComputeAssetReturns(
    FakeBacktestDataset dataset, IReadOnlyList<Symbol> assets)
{
    var result = new Dictionary<Symbol, decimal>();
    foreach (var asset in assets)
    {
        var closes = dataset.Prices.Values
            .Where(d => d.ContainsKey(asset))
            .Select(d => d[asset].Close)
            .ToArray();
        if (closes.Length >= 2)
        {
            result[asset] = closes[^1] / closes[0] - 1m;
        }
        else
        {
            result[asset] = 0m;
        }
    }
    return result;
}

// ── Types ─────────────────────────────────────────────────────────────────────

sealed record ExampleEntry(string Id, string Description, Func<Task> Run);

static class RandomExtensions
{
    public static double NextGaussian(this Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
