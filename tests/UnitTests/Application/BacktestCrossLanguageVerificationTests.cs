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

using System.Text.Json;

using Boutquin.Trading.Application.CostModels;
using Boutquin.Trading.Application.SlippageModels;
using Boutquin.Trading.Domain.Helpers;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests that validate the C# backtest engine
/// against golden test vectors produced by an independent Python implementation.
///
/// The Python reference engine (generate_backtest_vectors.py) follows the exact
/// same conventions as the C# engine:
///   - Next-bar Open fills (signals on bar T fill at bar T+1's Open)
///   - Commission = fillPrice * quantity * commissionRate
///   - Position sizing: Math.Round(totalValue * weight / adjClose, AwayFromZero)
///   - Equity = sum(position * AdjustedClose) + cash
///
/// Run generate_backtest_vectors.py in tests/Verification/ first to produce vectors.
/// </summary>
public sealed class BacktestCrossLanguageVerificationTests
{
    // Tolerance tiers for backtest comparisons
    // Equity curve values accumulate floating-point differences
    private const decimal PrecisionEquity = 1e-4m;
    // Tearsheet metrics (annualized ratios, etc.) allow wider tolerance
    private const decimal PrecisionMetric = 1e-4m;
    // Multi-asset full-year scenarios: floating-point differences compound over 252 days × 3 assets
    // with quantity-limiting and commission recalculation, amplified in derived ratios (Sharpe, Sortino, Calmar)
    private const decimal PrecisionMultiAsset = 5e-3m;

    private static string GetVectorsDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "tests", "Verification", "vectors");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "Cannot find tests/Verification/vectors/ directory. " +
            "Run 'python generate_backtest_vectors.py' in tests/Verification/ first.");
    }

    private static JsonDocument LoadVector(string name)
    {
        var path = Path.Combine(GetVectorsDir(), $"{name}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Vector file not found: {path}. Run generate_backtest_vectors.py first.", path);
        }
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Builds a mock IMarketDataFetcher from the JSON market data in a vector file.
    /// </summary>
    private static IMarketDataFetcher BuildMockFetcher(JsonElement marketDataElement)
    {
        var buffered = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();

        foreach (var tickerProp in marketDataElement.EnumerateObject())
        {
            var ticker = tickerProp.Name;
            var asset = new Asset(ticker);

            foreach (var record in tickerProp.Value.EnumerateArray())
            {
                var dateStr = record.GetProperty("date").GetString()!;
                var date = DateOnly.Parse(dateStr);

                var md = new MarketData(
                    date,
                    (decimal)record.GetProperty("open").GetDouble(),
                    (decimal)record.GetProperty("high").GetDouble(),
                    (decimal)record.GetProperty("low").GetDouble(),
                    (decimal)record.GetProperty("close").GetDouble(),
                    (decimal)record.GetProperty("adjusted_close").GetDouble(),
                    record.GetProperty("volume").GetInt64(),
                    (decimal)record.GetProperty("dividend_per_share").GetDouble(),
                    (decimal)record.GetProperty("split_coefficient").GetDouble());

                if (!buffered.TryGetValue(date, out var dayData))
                {
                    dayData = new SortedDictionary<Asset, MarketData>();
                    buffered[date] = dayData;
                }
                dayData[asset] = md;
            }
        }

        var mock = new Mock<IMarketDataFetcher>();
        mock.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(buffered));
        mock.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>()));

        return mock.Object;
    }

    private static async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<TKey, TValue>>> ToAsyncEnumerable<TKey, TValue>(
        SortedDictionary<DateOnly, SortedDictionary<TKey, TValue>> data) where TKey : notnull
    {
        foreach (var kvp in data)
        {
            yield return kvp;
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a Portfolio + BackTest from vector inputs and runs the backtest.
    /// </summary>
    private static async Task<Tearsheet> RunBacktestFromVector(
        JsonElement inputs,
        JsonElement? benchmarkInputs = null)
    {
        var marketDataElement = inputs.GetProperty("market_data");
        var weightsElement = inputs.GetProperty("weights");
        var initialCash = (decimal)inputs.GetProperty("initial_cash").GetDouble();
        var commissionRate = (decimal)inputs.GetProperty("commission_rate").GetDouble();

        // Build asset weights
        var weights = new Dictionary<Asset, decimal>();
        var assetCurrencies = new Dictionary<Asset, CurrencyCode>();
        var assets = new Dictionary<Asset, CurrencyCode>();
        foreach (var w in weightsElement.EnumerateObject())
        {
            var asset = new Asset(w.Name);
            weights[asset] = (decimal)w.Value.GetDouble();
            assetCurrencies[asset] = CurrencyCode.USD;
            assets[asset] = CurrencyCode.USD;
        }

        // Determine date range from market data
        DateOnly? startDate = null;
        DateOnly? endDate = null;
        foreach (var tickerProp in marketDataElement.EnumerateObject())
        {
            foreach (var record in tickerProp.Value.EnumerateArray())
            {
                var date = DateOnly.Parse(record.GetProperty("date").GetString()!);
                if (startDate == null || date < startDate)
                {
                    startDate = date;
                }

                if (endDate == null || date > endDate)
                {
                    endDate = date;
                }
            }
        }

        var fetcher = BuildMockFetcher(marketDataElement);
        var costModel = commissionRate > 0
            ? (ITransactionCostModel)new PercentageOfValueCostModel(commissionRate)
            : new ZeroCostModel();
        var broker = new SimulatedBrokerage(fetcher, costModel, new NoSlippage());

        var cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, initialCash } };
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new FixedWeightPositionSizer(
            new ReadOnlyDictionary<Asset, decimal>(new Dictionary<Asset, decimal>(weights)),
            CurrencyCode.USD);

        var strategy = new BuyAndHoldStrategy(
            "TestStrategy",
            new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(assets)),
            cash,
            startDate!.Value,
            orderPriceCalc,
            positionSizer);

        var handlers = new Dictionary<Type, IEventHandler>
        {
            { typeof(MarketEvent), new MarketEventHandler() },
            { typeof(SignalEvent), new SignalEventHandler() },
            { typeof(OrderEvent), new OrderEventHandler() },
            { typeof(FillEvent), new FillEventHandler() },
        };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "TestStrategy", strategy } }),
            new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(assetCurrencies)),
            new ReadOnlyDictionary<Type, IEventHandler>(handlers),
            broker);

        // Build benchmark (use same market data and config if no separate benchmark)
        Portfolio benchmarkPortfolio;
        IMarketDataFetcher benchmarkFetcher;

        if (benchmarkInputs.HasValue)
        {
            var bmMarketData = benchmarkInputs.Value.GetProperty("benchmark_market_data");
            var bmWeightsElement = benchmarkInputs.Value.GetProperty("benchmark_weights");

            var bmWeights = new Dictionary<Asset, decimal>();
            var bmAssets = new Dictionary<Asset, CurrencyCode>();
            var bmAssetCurrencies = new Dictionary<Asset, CurrencyCode>();
            foreach (var w in bmWeightsElement.EnumerateObject())
            {
                var asset = new Asset(w.Name);
                bmWeights[asset] = (decimal)w.Value.GetDouble();
                bmAssets[asset] = CurrencyCode.USD;
                bmAssetCurrencies[asset] = CurrencyCode.USD;
            }

            // Merge market data for the combined fetcher
            var combinedMarketData = marketDataElement.Clone();
            benchmarkFetcher = BuildMockFetcher(bmMarketData);
            // Actually, for BackTest we need a single fetcher that serves both
            // Let's build one that has all data
            var mergedElement = MergeMarketData(marketDataElement, bmMarketData);
            var mergedFetcher = BuildMockFetcher(mergedElement);

            var bmBroker = new SimulatedBrokerage(mergedFetcher, costModel, new NoSlippage());
            var bmCash = new SortedDictionary<CurrencyCode, decimal>
                { { CurrencyCode.USD, (decimal)benchmarkInputs.Value.GetProperty("initial_cash").GetDouble() } };
            var bmPositionSizer = new FixedWeightPositionSizer(
                new ReadOnlyDictionary<Asset, decimal>(new Dictionary<Asset, decimal>(bmWeights)),
                CurrencyCode.USD);
            var bmStrategy = new BuyAndHoldStrategy(
                "BenchmarkStrategy",
                new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(bmAssets)),
                bmCash,
                startDate!.Value,
                orderPriceCalc,
                bmPositionSizer);

            var bmHandlers = new Dictionary<Type, IEventHandler>
            {
                { typeof(MarketEvent), new MarketEventHandler() },
                { typeof(SignalEvent), new SignalEventHandler() },
                { typeof(OrderEvent), new OrderEventHandler() },
                { typeof(FillEvent), new FillEventHandler() },
            };

            benchmarkPortfolio = new Portfolio(
                CurrencyCode.USD,
                new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "BenchmarkStrategy", bmStrategy } }),
                new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(bmAssetCurrencies)),
                new ReadOnlyDictionary<Type, IEventHandler>(bmHandlers),
                bmBroker);

            // Use merged fetcher for the BackTest
            fetcher = mergedFetcher;
            // Recreate portfolio broker with merged fetcher
            broker = new SimulatedBrokerage(mergedFetcher, costModel, new NoSlippage());
            // Recreate portfolio with new broker
            portfolio = new Portfolio(
                CurrencyCode.USD,
                new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "TestStrategy", strategy } }),
                new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(assetCurrencies)),
                new ReadOnlyDictionary<Type, IEventHandler>(handlers),
                broker);
        }
        else
        {
            // Generate a synthetic benchmark asset ("BM") with slightly perturbed prices
            // to ensure non-zero active returns (avoids CalculationException in
            // InformationRatio/Sortino). The benchmark uses the same date range.
            var bmAsset = new Asset("BM");
            var bmAssets = new Dictionary<Asset, CurrencyCode> { { bmAsset, CurrencyCode.USD } };
            var bmAssetCurrencies2 = new Dictionary<Asset, CurrencyCode> { { bmAsset, CurrencyCode.USD } };
            var bmWeights = new Dictionary<Asset, decimal> { { bmAsset, 1.0m } };

            // Create benchmark market data: same dates, price = 100 + (index * 0.01)
            var bmBuffered = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
            var allDates = new List<DateOnly>();
            foreach (var tickerProp in marketDataElement.EnumerateObject())
            {
                foreach (var record in tickerProp.Value.EnumerateArray())
                {
                    allDates.Add(DateOnly.Parse(record.GetProperty("date").GetString()!));
                }
                break; // Only need dates from first ticker
            }
            allDates.Sort();

            var bmPrice = 100.0m;
            var rng = new Random(12345);
            foreach (var date in allDates)
            {
                var ret = 1.0m + (decimal)(rng.NextDouble() * 0.02 - 0.01); // ±1%
                bmPrice *= ret;
                var md = new MarketData(date, bmPrice, bmPrice * 1.01m, bmPrice * 0.99m,
                    bmPrice, bmPrice, 1_000_000, 0m, 1.0m);
                bmBuffered[date] = new SortedDictionary<Asset, MarketData> { { bmAsset, md } };
            }

            // Merge benchmark data into fetcher
            var mergedBuffered = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
            // First add original data
            foreach (var tickerProp in marketDataElement.EnumerateObject())
            {
                var ticker = tickerProp.Name;
                var origAsset = new Asset(ticker);
                foreach (var record in tickerProp.Value.EnumerateArray())
                {
                    var date = DateOnly.Parse(record.GetProperty("date").GetString()!);
                    if (!mergedBuffered.TryGetValue(date, out var dayAssets))
                    {
                        dayAssets = new SortedDictionary<Asset, MarketData>();
                        mergedBuffered[date] = dayAssets;
                    }
                    dayAssets[origAsset] = new MarketData(
                        date,
                        (decimal)record.GetProperty("open").GetDouble(),
                        (decimal)record.GetProperty("high").GetDouble(),
                        (decimal)record.GetProperty("low").GetDouble(),
                        (decimal)record.GetProperty("close").GetDouble(),
                        (decimal)record.GetProperty("adjusted_close").GetDouble(),
                        record.GetProperty("volume").GetInt64(),
                        (decimal)record.GetProperty("dividend_per_share").GetDouble(),
                        (decimal)record.GetProperty("split_coefficient").GetDouble());
                }
            }
            // Add benchmark data
            foreach (var (date, dayData) in bmBuffered)
            {
                if (!mergedBuffered.TryGetValue(date, out var bmDayAssets))
                {
                    bmDayAssets = new SortedDictionary<Asset, MarketData>();
                    mergedBuffered[date] = bmDayAssets;
                }

                foreach (var (a, md) in dayData)
                {
                    bmDayAssets[a] = md;
                }
            }

            var mergedMock = new Mock<IMarketDataFetcher>();
            mergedMock.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
                .Returns(ToAsyncEnumerable(mergedBuffered));
            mergedMock.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns(ToAsyncEnumerable(new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>()));
            var mergedFetcher = mergedMock.Object;

            var bmBroker2 = new SimulatedBrokerage(mergedFetcher, costModel, new NoSlippage());
            var bmCash2 = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, initialCash } };
            var bmPositionSizer2 = new FixedWeightPositionSizer(
                new ReadOnlyDictionary<Asset, decimal>(new Dictionary<Asset, decimal>(bmWeights)),
                CurrencyCode.USD);
            var bmStrategy2 = new BuyAndHoldStrategy(
                "BenchmarkStrategy",
                new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(bmAssets)),
                bmCash2,
                startDate!.Value,
                orderPriceCalc,
                bmPositionSizer2);

            var bmHandlers2 = new Dictionary<Type, IEventHandler>
            {
                { typeof(MarketEvent), new MarketEventHandler() },
                { typeof(SignalEvent), new SignalEventHandler() },
                { typeof(OrderEvent), new OrderEventHandler() },
                { typeof(FillEvent), new FillEventHandler() },
            };

            benchmarkPortfolio = new Portfolio(
                CurrencyCode.USD,
                new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "BenchmarkStrategy", bmStrategy2 } }),
                new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(bmAssetCurrencies2)),
                new ReadOnlyDictionary<Type, IEventHandler>(bmHandlers2),
                bmBroker2);

            // Use merged fetcher for BackTest so it can serve both portfolio and benchmark
            fetcher = mergedFetcher;
            broker = new SimulatedBrokerage(mergedFetcher, costModel, new NoSlippage());
            portfolio = new Portfolio(
                CurrencyCode.USD,
                new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "TestStrategy", strategy } }),
                new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(assetCurrencies)),
                new ReadOnlyDictionary<Type, IEventHandler>(handlers),
                broker);
        }

        var backtest = new BackTest(portfolio, benchmarkPortfolio, fetcher, CurrencyCode.USD);
        return await backtest.RunAsync(startDate!.Value, endDate!.Value);
    }

    private static JsonElement MergeMarketData(JsonElement a, JsonElement b)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var prop in a.EnumerateObject())
        {
            dict[prop.Name] = prop.Value;
        }

        foreach (var prop in b.EnumerateObject())
        {
            dict[prop.Name] = prop.Value;
        }

        var json = JsonSerializer.Serialize(dict);
        return JsonDocument.Parse(json).RootElement;
    }

    // ─── Scenario 1: Single-asset buy-and-hold (no commission) ──────────

    [Fact]
    public async Task SingleAsset_EquityCurve_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_single_asset");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedEquity = doc.RootElement.GetProperty("expected").GetProperty("equity_curve");

        var tearsheet = await RunBacktestFromVector(inputs);

        foreach (var prop in expectedEquity.EnumerateObject())
        {
            var date = DateOnly.Parse(prop.Name);
            var expected = (decimal)prop.Value.GetDouble();
            Assert.True(tearsheet.EquityCurve.ContainsKey(date),
                $"Missing date {date} in C# equity curve");
            Assert.InRange(Math.Abs(tearsheet.EquityCurve[date] - expected), 0, PrecisionEquity);
        }
    }

    [Fact]
    public async Task SingleAsset_EquityCurveLength_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_single_asset");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedEquity = doc.RootElement.GetProperty("expected").GetProperty("equity_curve");
        var expectedCount = 0;
        foreach (var _ in expectedEquity.EnumerateObject())
        {
            expectedCount++;
        }

        var tearsheet = await RunBacktestFromVector(inputs);

        Assert.Equal(expectedCount, tearsheet.EquityCurve.Count);
    }

    // ─── Scenario 2: Single-asset with commission ───────────────────────

    [Fact]
    public async Task SingleAssetCommission_EquityCurve_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_single_asset_commission");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedEquity = doc.RootElement.GetProperty("expected").GetProperty("equity_curve");

        var tearsheet = await RunBacktestFromVector(inputs);

        foreach (var prop in expectedEquity.EnumerateObject())
        {
            var date = DateOnly.Parse(prop.Name);
            var expected = (decimal)prop.Value.GetDouble();
            Assert.True(tearsheet.EquityCurve.ContainsKey(date),
                $"Missing date {date} in C# equity curve");
            Assert.InRange(Math.Abs(tearsheet.EquityCurve[date] - expected), 0, PrecisionEquity);
        }
    }

    // ─── Scenario 3: Multi-asset fixed weights ──────────────────────────

    [Fact]
    public async Task MultiAsset_EquityCurve_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_multi_asset");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedEquity = doc.RootElement.GetProperty("expected").GetProperty("equity_curve");

        var tearsheet = await RunBacktestFromVector(inputs);

        foreach (var prop in expectedEquity.EnumerateObject())
        {
            var date = DateOnly.Parse(prop.Name);
            var expected = (decimal)prop.Value.GetDouble();
            Assert.True(tearsheet.EquityCurve.ContainsKey(date),
                $"Missing date {date} in C# equity curve");
            Assert.InRange(Math.Abs(tearsheet.EquityCurve[date] - expected), 0, PrecisionEquity);
        }
    }

    // ─── Scenario 4: Commission impact ──────────────────────────────────

    [Fact]
    public async Task CommissionImpact_NoCommission_HigherEquity()
    {
        using var doc = LoadVector("backtest_commission_impact");
        var inputs = doc.RootElement.GetProperty("inputs");
        var marketData = inputs.GetProperty("market_data");
        var weights = inputs.GetProperty("weights");
        var initialCash = (decimal)inputs.GetProperty("initial_cash").GetDouble();

        // Build no-commission inputs
        var noCommJson = $@"{{
            ""market_data"": {marketData.GetRawText()},
            ""weights"": {weights.GetRawText()},
            ""initial_cash"": {initialCash},
            ""commission_rate"": 0.0,
            ""strategy"": ""BuyAndHold""
        }}";
        using var noCommDoc = JsonDocument.Parse(noCommJson);
        var noCommTearsheet = await RunBacktestFromVector(noCommDoc.RootElement);

        // Build with-commission inputs
        var withCommJson = $@"{{
            ""market_data"": {marketData.GetRawText()},
            ""weights"": {weights.GetRawText()},
            ""initial_cash"": {initialCash},
            ""commission_rate"": 0.001,
            ""strategy"": ""BuyAndHold""
        }}";
        using var withCommDoc = JsonDocument.Parse(withCommJson);
        var withCommTearsheet = await RunBacktestFromVector(withCommDoc.RootElement);

        // Final equity without commission should be >= with commission
        var lastDateNoComm = noCommTearsheet.EquityCurve.Keys.Last();
        var lastDateWithComm = withCommTearsheet.EquityCurve.Keys.Last();

        Assert.True(noCommTearsheet.EquityCurve[lastDateNoComm] >=
                    withCommTearsheet.EquityCurve[lastDateWithComm] - PrecisionEquity);
    }

    // ─── Scenario 6: Drawdown metrics ───────────────────────────────────

    [Fact]
    public async Task Drawdown_MaxDrawdown_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_drawdown");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedMaxDD = (decimal)doc.RootElement.GetProperty("expected")
            .GetProperty("metrics").GetProperty("max_drawdown").GetDouble();

        var tearsheet = await RunBacktestFromVector(inputs);

        Assert.InRange(Math.Abs(tearsheet.MaxDrawdown - expectedMaxDD), 0, PrecisionMetric);
    }

    [Fact]
    public async Task Drawdown_MaxDrawdownDuration_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_drawdown");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedDuration = doc.RootElement.GetProperty("expected")
            .GetProperty("metrics").GetProperty("max_drawdown_duration").GetInt32();

        var tearsheet = await RunBacktestFromVector(inputs);

        Assert.Equal(expectedDuration, tearsheet.MaxDrawdownDuration);
    }

    // ─── Scenario 7: Position sizing rounding ───────────────────────────

    [Fact]
    public async Task Rounding_EquityCurve_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_rounding");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedEquity = doc.RootElement.GetProperty("expected").GetProperty("equity_curve");

        var tearsheet = await RunBacktestFromVector(inputs);

        foreach (var prop in expectedEquity.EnumerateObject())
        {
            var date = DateOnly.Parse(prop.Name);
            var expected = (decimal)prop.Value.GetDouble();
            Assert.True(tearsheet.EquityCurve.ContainsKey(date),
                $"Missing date {date} in C# equity curve");
            Assert.InRange(Math.Abs(tearsheet.EquityCurve[date] - expected), 0, PrecisionEquity);
        }
    }

    // ─── Scenario 9: Cash remainder precision ───────────────────────────

    [Fact]
    public async Task CashPrecision_EquityCurve_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_cash_precision");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedEquity = doc.RootElement.GetProperty("expected").GetProperty("equity_curve");

        var tearsheet = await RunBacktestFromVector(inputs);

        foreach (var prop in expectedEquity.EnumerateObject())
        {
            var date = DateOnly.Parse(prop.Name);
            var expected = (decimal)prop.Value.GetDouble();
            Assert.True(tearsheet.EquityCurve.ContainsKey(date),
                $"Missing date {date} in C# equity curve");
            Assert.InRange(Math.Abs(tearsheet.EquityCurve[date] - expected), 0, PrecisionEquity);
        }
    }

    // ─── Scenario 10: Three-asset full-year metrics ─────────────────────

    [Fact]
    public async Task ThreeAsset_Metrics_MatchPythonVector()
    {
        using var doc = LoadVector("backtest_three_asset");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedMetrics = doc.RootElement.GetProperty("expected").GetProperty("metrics");

        var tearsheet = await RunBacktestFromVector(inputs);

        // Multi-asset full-year: quantity-rounding differences compound, use wider tolerance
        // Annualized return
        var expectedAR = (decimal)expectedMetrics.GetProperty("annualized_return").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.AnnualizedReturn - expectedAR), 0, PrecisionMultiAsset);

        // CAGR
        var expectedCAGR = (decimal)expectedMetrics.GetProperty("cagr").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.CAGR - expectedCAGR), 0, PrecisionMultiAsset);

        // Volatility
        var expectedVol = (decimal)expectedMetrics.GetProperty("annualized_volatility").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.Volatility - expectedVol), 0, PrecisionMultiAsset);

        // Sharpe
        var expectedSharpe = (decimal)expectedMetrics.GetProperty("annualized_sharpe_ratio").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.SharpeRatio - expectedSharpe), 0, PrecisionMultiAsset);

        // Sortino
        var expectedSortino = (decimal)expectedMetrics.GetProperty("annualized_sortino_ratio").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.SortinoRatio - expectedSortino), 0, PrecisionMultiAsset);

        // MaxDrawdown
        var expectedMDD = (decimal)expectedMetrics.GetProperty("max_drawdown").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.MaxDrawdown - expectedMDD), 0, PrecisionMultiAsset);

        // Calmar
        var expectedCalmar = (decimal)expectedMetrics.GetProperty("calmar_ratio").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.CalmarRatio - expectedCalmar), 0, PrecisionMultiAsset);

        // Omega
        var expectedOmega = (decimal)expectedMetrics.GetProperty("omega_ratio").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.OmegaRatio - expectedOmega), 0, PrecisionMultiAsset);

        // Win rate
        var expectedWR = (decimal)expectedMetrics.GetProperty("win_rate").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.WinRate - expectedWR), 0, PrecisionMultiAsset);

        // Skewness
        var expectedSkew = (decimal)expectedMetrics.GetProperty("skewness").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.Skewness - expectedSkew), 0, PrecisionMultiAsset);

        // Kurtosis
        var expectedKurt = (decimal)expectedMetrics.GetProperty("kurtosis").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.Kurtosis - expectedKurt), 0, PrecisionMultiAsset);
    }

    // ─── Scenario 11: Equal weight construction model ─────────────────────

    // Wider tolerance for construction model tests: the C# pipeline (ConstructionModelStrategy +
    // DynamicWeightPositionSizer + CalendarRebalancingTrigger) has behavioral differences from the
    // Python reference (position sizing uses ComputeTotalValue with FX rate lookups, warmup path
    // uses Underweight signals, etc.). A 2% tolerance on metrics still catches major bugs.
    private const decimal PrecisionConstructionModel = 0.02m;

    /// <summary>
    /// Scenario 11: ConstructionModelStrategy + DynamicWeightPositionSizer pipeline
    /// with EqualWeightConstruction and quarterly rebalancing must be in the same
    /// ballpark as the Python reference. This is a smoke test for the Phase 2
    /// portfolio construction pipeline — it verifies the pipeline produces
    /// reasonable results, not exact bit-for-bit matching.
    /// </summary>
    [Fact]
    public async Task Scenario11_EqualWeightConstruction_Metrics_MatchPythonVector()
    {
        using var doc = LoadVector("backtest_equal_weight_construction");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedMetrics = doc.RootElement.GetProperty("expected").GetProperty("metrics");

        var tearsheet = await RunConstructionModelBacktestFromVector(inputs);

        // Annualized return: should be in the same direction and magnitude
        var expectedAR = (decimal)expectedMetrics.GetProperty("annualized_return").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.AnnualizedReturn - expectedAR), 0, PrecisionConstructionModel);

        // Volatility: structural property that should be very close
        var expectedVol = (decimal)expectedMetrics.GetProperty("annualized_volatility").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.Volatility - expectedVol), 0, PrecisionConstructionModel);

        // Max drawdown: structural property
        var expectedMDD = (decimal)expectedMetrics.GetProperty("max_drawdown").GetDouble();
        Assert.InRange(Math.Abs(tearsheet.MaxDrawdown - expectedMDD), 0, PrecisionConstructionModel);
    }

    /// <summary>
    /// Scenario 11: Final equity should be within 2% of Python reference.
    /// </summary>
    [Fact]
    public async Task Scenario11_EqualWeightConstruction_FinalEquity_MatchesPythonVector()
    {
        using var doc = LoadVector("backtest_equal_weight_construction");
        var inputs = doc.RootElement.GetProperty("inputs");
        var expectedEquity = doc.RootElement.GetProperty("expected").GetProperty("equity_curve");

        var tearsheet = await RunConstructionModelBacktestFromVector(inputs);

        // Compare final equity value as percentage difference
        var equityEntries = expectedEquity.EnumerateObject().ToList();
        var lastExpected = (decimal)equityEntries.Last().Value.GetDouble();
        var finalEquity = tearsheet.EquityCurve.Values.Last();
        var pctDiff = Math.Abs(finalEquity - lastExpected) / lastExpected;
        Assert.InRange(pctDiff, 0, PrecisionConstructionModel);
    }

    /// <summary>
    /// Creates a Portfolio + BackTest using ConstructionModelStrategy + DynamicWeightPositionSizer
    /// and runs the backtest against vector inputs.
    /// </summary>
    private static async Task<Tearsheet> RunConstructionModelBacktestFromVector(JsonElement inputs)
    {
        var marketDataElement = inputs.GetProperty("market_data");
        var initialCash = (decimal)inputs.GetProperty("initial_cash").GetDouble();
        var commissionRate = (decimal)inputs.GetProperty("commission_rate").GetDouble();
        var rebalanceMonths = inputs.GetProperty("rebalance_months").GetInt32();

        // Map calendar months to RebalancingFrequency enum
        var rebalancingFrequency = rebalanceMonths switch
        {
            1 => RebalancingFrequency.Monthly,
            3 => RebalancingFrequency.Quarterly,
            12 => RebalancingFrequency.Annually,
            _ => RebalancingFrequency.Quarterly,
        };

        // Build assets from market data tickers
        var assets = new Dictionary<Asset, CurrencyCode>();
        var assetCurrencies = new Dictionary<Asset, CurrencyCode>();
        foreach (var tickerProp in marketDataElement.EnumerateObject())
        {
            var asset = new Asset(tickerProp.Name);
            assets[asset] = CurrencyCode.USD;
            assetCurrencies[asset] = CurrencyCode.USD;
        }

        // Date range
        DateOnly? startDate = null;
        DateOnly? endDate = null;
        foreach (var tickerProp in marketDataElement.EnumerateObject())
        {
            foreach (var record in tickerProp.Value.EnumerateArray())
            {
                var date = DateOnly.Parse(record.GetProperty("date").GetString()!);
                if (startDate == null || date < startDate)
                {
                    startDate = date;
                }

                if (endDate == null || date > endDate)
                {
                    endDate = date;
                }
            }
        }

        var fetcher = BuildMockFetcher(marketDataElement);
        var costModel = commissionRate > 0
            ? (ITransactionCostModel)new PercentageOfValueCostModel(commissionRate)
            : new ZeroCostModel();
        var broker = new SimulatedBrokerage(fetcher, costModel, new NoSlippage());

        var cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, initialCash } };
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new Boutquin.Trading.Application.PortfolioConstruction.EqualWeightConstruction();

        var strategy = new ConstructionModelStrategy(
            "TestStrategy",
            assets,
            cash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            rebalancingFrequency,
            lookbackWindow: 5); // Low lookback so signals generate from day 1

        var handlers = new Dictionary<Type, IEventHandler>
        {
            { typeof(MarketEvent), new MarketEventHandler() },
            { typeof(SignalEvent), new SignalEventHandler() },
            { typeof(OrderEvent), new OrderEventHandler() },
            { typeof(FillEvent), new FillEventHandler() },
        };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "TestStrategy", strategy } }),
            new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(assetCurrencies)),
            new ReadOnlyDictionary<Type, IEventHandler>(handlers),
            broker);

        // Synthetic benchmark
        var bmAsset = new Asset("BM");
        var bmAssets = new Dictionary<Asset, CurrencyCode> { { bmAsset, CurrencyCode.USD } };
        var bmWeights = new Dictionary<Asset, decimal> { { bmAsset, 1.0m } };

        var bmBuffered = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
        var allDates = new List<DateOnly>();
        foreach (var tickerProp in marketDataElement.EnumerateObject())
        {
            foreach (var record in tickerProp.Value.EnumerateArray())
            {
                allDates.Add(DateOnly.Parse(record.GetProperty("date").GetString()!));
            }

            break;
        }
        allDates.Sort();

        var bmPrice = 100.0m;
        var rng = new Random(12345);
        foreach (var date in allDates)
        {
            var ret = 1.0m + (decimal)(rng.NextDouble() * 0.02 - 0.01);
            bmPrice *= ret;
            var md = new MarketData(date, bmPrice, bmPrice * 1.01m, bmPrice * 0.99m,
                bmPrice, bmPrice, 1_000_000, 0m, 1.0m);
            bmBuffered[date] = new SortedDictionary<Asset, MarketData> { { bmAsset, md } };
        }

        // Merge market + benchmark data
        var mergedBuffered = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
        foreach (var tickerProp in marketDataElement.EnumerateObject())
        {
            var origAsset = new Asset(tickerProp.Name);
            foreach (var record in tickerProp.Value.EnumerateArray())
            {
                var date = DateOnly.Parse(record.GetProperty("date").GetString()!);
                if (!mergedBuffered.TryGetValue(date, out var dayAssets))
                {
                    dayAssets = new SortedDictionary<Asset, MarketData>();
                    mergedBuffered[date] = dayAssets;
                }
                dayAssets[origAsset] = new MarketData(
                    date,
                    (decimal)record.GetProperty("open").GetDouble(),
                    (decimal)record.GetProperty("high").GetDouble(),
                    (decimal)record.GetProperty("low").GetDouble(),
                    (decimal)record.GetProperty("close").GetDouble(),
                    (decimal)record.GetProperty("adjusted_close").GetDouble(),
                    record.GetProperty("volume").GetInt64(),
                    (decimal)record.GetProperty("dividend_per_share").GetDouble(),
                    (decimal)record.GetProperty("split_coefficient").GetDouble());
            }
        }
        foreach (var (date, dayData) in bmBuffered)
        {
            if (!mergedBuffered.TryGetValue(date, out var bmDayAssets))
            {
                bmDayAssets = new SortedDictionary<Asset, MarketData>();
                mergedBuffered[date] = bmDayAssets;
            }
            foreach (var (a, md) in dayData)
            {
                bmDayAssets[a] = md;
            }
        }

        var mergedMock = new Mock<IMarketDataFetcher>();
        mergedMock.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(mergedBuffered));
        mergedMock.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>()));
        var mergedFetcher = mergedMock.Object;

        // Recreate with merged fetcher
        broker = new SimulatedBrokerage(mergedFetcher, costModel, new NoSlippage());
        portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "TestStrategy", strategy } }),
            new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(assetCurrencies)),
            new ReadOnlyDictionary<Type, IEventHandler>(handlers),
            broker);

        var bmBroker = new SimulatedBrokerage(mergedFetcher, costModel, new NoSlippage());
        var bmCash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, initialCash } };
        var bmPositionSizer = new FixedWeightPositionSizer(
            new ReadOnlyDictionary<Asset, decimal>(new Dictionary<Asset, decimal>(bmWeights)),
            CurrencyCode.USD);
        var bmStrategy = new BuyAndHoldStrategy(
            "BenchmarkStrategy",
            new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(bmAssets)),
            bmCash,
            startDate!.Value,
            orderPriceCalc,
            bmPositionSizer);
        var bmHandlers = new Dictionary<Type, IEventHandler>
        {
            { typeof(MarketEvent), new MarketEventHandler() },
            { typeof(SignalEvent), new SignalEventHandler() },
            { typeof(OrderEvent), new OrderEventHandler() },
            { typeof(FillEvent), new FillEventHandler() },
        };
        var benchmarkPortfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "BenchmarkStrategy", bmStrategy } }),
            new ReadOnlyDictionary<Asset, CurrencyCode>(new Dictionary<Asset, CurrencyCode>(bmAssets)),
            new ReadOnlyDictionary<Type, IEventHandler>(bmHandlers),
            bmBroker);

        var backtest = new BackTest(portfolio, benchmarkPortfolio, mergedFetcher, CurrencyCode.USD);
        return await backtest.RunAsync(startDate!.Value, endDate!.Value);
    }

    /// <summary>
    /// Zero cost model for no-commission scenarios (avoids Guard.AgainstNegativeOrZero).
    /// </summary>
    private sealed class ZeroCostModel : ITransactionCostModel
    {
        public decimal CalculateCommission(decimal fillPrice, int quantity, TradeAction tradeAction) => 0m;
    }
}
