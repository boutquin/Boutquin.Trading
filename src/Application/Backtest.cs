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

namespace Boutquin.Trading.Application;

/// <summary>
/// Represents a BackTest of a trading portfolio with multiple assets, strategies, and events.
/// The BackTest class is responsible for running the BackTest by iterating through a series of events,
/// processing the events for each strategy, and updating the portfolio state. It also analyzes the performance
/// metrics of the portfolio, comparing it with a benchmark strategy.
/// </summary>
public sealed class BackTest
{
    /// <summary>
    /// The portfolio to use for the backtesting simulation, represented
    /// as a Portfolio object.
    /// </summary>
    private readonly IPortfolio _portfolio;

    /// <summary>
    /// The benchmark portfolio to use for the backtesting simulation,
    /// represented as a Portfolio object.
    /// </summary>
    private readonly IPortfolio _benchmarkPortfolio;

    /// <summary>
    /// The market data source to use for loading historical market data
    /// and dividend data, represented as an IMarketDataFetcher object.
    /// </summary>
    private readonly IMarketDataFetcher _marketDataFetcher;

    /// <summary>
    /// The base currency for the backtesting simulation.
    /// </summary>
    private readonly CurrencyCode _baseCurrency;

    private readonly ILogger<BackTest> _logger;

    private readonly decimal _dailyRiskFreeRate;

    private readonly IDrawdownControl? _drawdownControl;

    private readonly ITradingCalendar? _tradingCalendar;

    private readonly decimal _defaultDailyExpenseRate;
    private readonly IReadOnlyDictionary<string, decimal> _perAssetDailyExpenseRates;

    /// <summary>
    /// Initializes a new instance of the BackTest class (backward-compatible overload).
    /// </summary>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, IMarketDataFetcher marketDataFetcher, CurrencyCode baseCurrency)
        : this(portfolio, benchmarkPortfolio, marketDataFetcher, baseCurrency, NullLogger<BackTest>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with structured logging.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="marketDataFetcher">An object implementing the IMarketDataFetcher interface, responsible for providing market data for the backtest.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency for the backtest.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the provided arguments are null.</exception>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, IMarketDataFetcher marketDataFetcher, CurrencyCode baseCurrency, ILogger<BackTest> logger)
    {
        _portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio), "The provided portfolio cannot be null.");
        _benchmarkPortfolio = benchmarkPortfolio ?? throw new ArgumentNullException(nameof(benchmarkPortfolio), "The provided benchmark portfolio cannot be null.");
        _marketDataFetcher = marketDataFetcher ?? throw new ArgumentNullException(nameof(marketDataFetcher), "The provided market reader source cannot be null.");
        _baseCurrency = baseCurrency;
        _logger = logger ?? NullLogger<BackTest>.Instance;
        _perAssetDailyExpenseRates = new Dictionary<string, decimal>();
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with structured logging and a risk-free rate.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="marketDataFetcher">An object implementing the IMarketDataFetcher interface, responsible for providing market data for the backtest.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency for the backtest.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <param name="dailyRiskFreeRate">The daily risk-free rate as a decimal (e.g., 0.05/252 for 5% annualized). Default: 0.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the provided arguments are null.</exception>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, IMarketDataFetcher marketDataFetcher, CurrencyCode baseCurrency, ILogger<BackTest> logger, decimal dailyRiskFreeRate)
        : this(portfolio, benchmarkPortfolio, marketDataFetcher, baseCurrency, logger)
    {
        _dailyRiskFreeRate = dailyRiskFreeRate;
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with drawdown control.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="marketDataFetcher">An object implementing the IMarketDataFetcher interface.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <param name="dailyRiskFreeRate">The daily risk-free rate as a decimal.</param>
    /// <param name="drawdownControl">Optional daily drawdown monitor and circuit breaker.</param>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, IMarketDataFetcher marketDataFetcher, CurrencyCode baseCurrency, ILogger<BackTest> logger, decimal dailyRiskFreeRate, IDrawdownControl? drawdownControl)
        : this(portfolio, benchmarkPortfolio, marketDataFetcher, baseCurrency, logger, dailyRiskFreeRate)
    {
        _drawdownControl = drawdownControl;
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with trading calendar support.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="marketDataFetcher">An object implementing the IMarketDataFetcher interface.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <param name="dailyRiskFreeRate">The daily risk-free rate as a decimal.</param>
    /// <param name="drawdownControl">Optional daily drawdown monitor and circuit breaker.</param>
    /// <param name="tradingCalendar">Optional trading calendar for non-trading-day filtering and market-aware annualization.</param>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, IMarketDataFetcher marketDataFetcher, CurrencyCode baseCurrency, ILogger<BackTest> logger, decimal dailyRiskFreeRate, IDrawdownControl? drawdownControl, ITradingCalendar? tradingCalendar)
        : this(portfolio, benchmarkPortfolio, marketDataFetcher, baseCurrency, logger, dailyRiskFreeRate, drawdownControl)
    {
        _tradingCalendar = tradingCalendar;
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with expense ratio support.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="marketDataFetcher">An object implementing the IMarketDataFetcher interface.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <param name="dailyRiskFreeRate">The daily risk-free rate as a decimal.</param>
    /// <param name="drawdownControl">Optional daily drawdown monitor and circuit breaker.</param>
    /// <param name="tradingCalendar">Optional trading calendar for non-trading-day filtering.</param>
    /// <param name="annualExpenseRatioBps">Default annual expense ratio in basis points (e.g., 20 = 0.20%). Applied to assets without a per-asset override.</param>
    /// <param name="assetExpenseRatiosBps">Per-asset annual expense ratios in basis points, keyed by ticker. Overrides the default for specified assets.</param>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, IMarketDataFetcher marketDataFetcher, CurrencyCode baseCurrency, ILogger<BackTest> logger, decimal dailyRiskFreeRate, IDrawdownControl? drawdownControl, ITradingCalendar? tradingCalendar, decimal annualExpenseRatioBps, IReadOnlyDictionary<string, decimal>? assetExpenseRatiosBps = null)
        : this(portfolio, benchmarkPortfolio, marketDataFetcher, baseCurrency, logger, dailyRiskFreeRate, drawdownControl, tradingCalendar)
    {
        if (annualExpenseRatioBps < 0 || annualExpenseRatioBps > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(annualExpenseRatioBps), annualExpenseRatioBps,
                "Annual expense ratio must be between 0 and 1000 basis points (0%–10%).");
        }

        if (assetExpenseRatiosBps is not null)
        {
            foreach (var (ticker, bps) in assetExpenseRatiosBps)
            {
                if (bps < 0 || bps > 1000)
                {
                    throw new ArgumentOutOfRangeException(nameof(assetExpenseRatiosBps),
                        $"Per-asset expense ratio for '{ticker}' must be between 0 and 1000 basis points, got {bps}.");
                }
            }
        }

        _defaultDailyExpenseRate = annualExpenseRatioBps / 10_000m / 252m;
        _perAssetDailyExpenseRates = assetExpenseRatiosBps?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value / 10_000m / 252m)
            ?? new Dictionary<string, decimal>();
    }

    /// <summary>
    /// Runs the backtest simulation asynchronously for the specified start and end dates.
    /// </summary>
    /// <param name="startDate">A DateOnly object representing the start date of the backtest simulation.</param>
    /// <param name="endDate">A DateOnly object representing the end date of the backtest simulation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="burnInEndDate">Optional burn-in end date. When set, the equity curve is only updated for dates
    /// after this date, allowing indicators and strategies to warm up before official performance tracking begins.
    /// Must satisfy: startDate &lt; burnInEndDate &lt; endDate.</param>
    /// <param name="afterDayCallback">Optional callback invoked after each trading day is processed.</param>
    /// <returns>A Task that represents the asynchronous operation. The task result contains a Tearsheet object containing various performance metrics for the backtested portfolio and benchmark portfolio.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided start date is greater than or equal to the end date, or when burnInEndDate is outside the valid range.</exception>
    public async Task<Tearsheet> RunAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default, DateOnly? burnInEndDate = null, Func<IPortfolio, CancellationToken, Task>? afterDayCallback = null)
    {
        // Validate the start and end dates.
        if (startDate >= endDate)
        {
            throw new ArgumentException("The start date must be earlier than the end date.", nameof(startDate));
        }

        // Validate burn-in end date: must be strictly between startDate and endDate.
        if (burnInEndDate.HasValue)
        {
            if (burnInEndDate.Value <= startDate || burnInEndDate.Value >= endDate)
            {
                throw new ArgumentException(
                    $"The burn-in end date ({burnInEndDate.Value}) must be strictly between startDate ({startDate}) and endDate ({endDate}).",
                    nameof(burnInEndDate));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Backtest starting: {StartDate} to {EndDate}", startDate, endDate);

        if (burnInEndDate.HasValue)
        {
            _logger.LogInformation("Burn-in period active: equity curve tracking starts after {BurnInEndDate}", burnInEndDate.Value);
        }

        // Fetch the historical market data for the backtest period for both the portfolio and the benchmark portfolio.
        // L1: .Union() already deduplicates — removed redundant .Distinct()
        var symbols = _portfolio.Strategies.Values.SelectMany(s => s.Assets.Keys)
                      .Union(_benchmarkPortfolio.Strategies.Values.SelectMany(s => s.Assets.Keys));

        // Fetch and materialize market data once — eliminates double-streaming
        // and enables the buffered dictionary to be passed to SimulatedBrokerage
        var marketDataTimeline = _marketDataFetcher.FetchMarketDataAsync(symbols, cancellationToken);
        var bufferedMarketData = new SortedDictionary<DateOnly, SortedDictionary<Domain.ValueObjects.Asset, MarketData>>();

        await foreach (var kvp in marketDataTimeline.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            // BUG-A07: Filter market data to startDate..endDate range
            if (kvp.Key >= startDate && kvp.Key <= endDate)
            {
                // Merge — fetchers emit one-symbol-per-date entries; overwrite would drop all but the last.
                if (bufferedMarketData.TryGetValue(kvp.Key, out var existing))
                {
                    foreach (var entry in kvp.Value)
                    {
                        existing[entry.Key] = entry.Value;
                    }
                }
                else
                {
                    bufferedMarketData[kvp.Key] = kvp.Value;
                }
            }
        }

        // H3: Include both portfolio AND benchmark asset currencies for FX rate fetching.
        // Filter out same-currency pairs (e.g. USD_USD) — FX providers reject them with HTTP 422.
        var currencyPairList = _portfolio.Strategies.Values
                                         .Concat(_benchmarkPortfolio.Strategies.Values)
                                         .SelectMany(s => s.Assets.Values)
                                         .Where(currencyCode => currencyCode != _baseCurrency)
                                         .Select(currencyCode => $"{_baseCurrency}_{currencyCode}")
                                         .Distinct()
                                         .ToList();

        // Create a dictionary to hold the FX rates for each date.
        var fxRatesForDate = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();

        // Skip the FX fetch entirely for single-currency portfolios (e.g. all-USD) —
        // passing an empty list to the fetcher throws ArgumentException.
        if (currencyPairList.Count > 0)
        {
            var fxRatesTimeline = _marketDataFetcher.FetchFxRatesAsync(currencyPairList, cancellationToken);
            await foreach (var fxRatesOnDate in fxRatesTimeline.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                fxRatesForDate[fxRatesOnDate.Key] = fxRatesOnDate.Value;
            }

            // M32: Warn when FX fetch was attempted but returned no rates — likely a data gap.
            if (fxRatesForDate.Count == 0)
            {
                _logger.LogWarning("No FX conversion rates loaded. Foreign currency assets may fail valuation.");
            }
        }

        // Event loop iterates buffered market data — single materialization
        foreach (var marketData in bufferedMarketData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip non-trading days when calendar is provided (defensive filter)
            if (_tradingCalendar is not null && !_tradingCalendar.IsTradingDay(marketData.Key))
            {
                _logger.LogWarning("Skipping non-trading day {Date} — market data present but date is not a trading day per calendar", marketData.Key);
                continue;
            }

            // Process pending orders from the previous bar at today's Open price.
            // This eliminates look-ahead bias: signals generated on bar T fill at bar T+1.
            foreach (var portfolio in new[] { _portfolio, _benchmarkPortfolio })
            {
                await portfolio.ProcessPendingOrdersAsync(marketData.Key, marketData.Value, cancellationToken).ConfigureAwait(false);
            }

            // Get the FX rates for the current date.
            var fxRates = fxRatesForDate.TryGetValue(marketData.Key, out var ratesForDate)
                          ? ratesForDate
                          : []; // Use an empty dictionary if there are no rates for this date.

            // Generate a MarketEvent for the current day's market data.
            var marketEvent = new MarketEvent(
                marketData.Key,
                marketData.Value,
                fxRates
            );

            // Handle the MarketEvent for each strategy in the portfolio.
            // This updates historical data, generates signals, and queues new orders for next bar.
            foreach (var portfolio in new[] { _portfolio, _benchmarkPortfolio })
            {
                await portfolio.HandleEventAsync(marketEvent, cancellationToken).ConfigureAwait(false);

                // Deduct daily expense ratio — portfolio only, not benchmark.
                // Benchmark tracks index performance without the portfolio's fee structure.
                if (portfolio == _portfolio && (_defaultDailyExpenseRate > 0 || _perAssetDailyExpenseRates.Count > 0))
                {
                    ApplyDailyExpenseDeduction(portfolio, marketData.Key, marketData.Value);
                }

                // Only update equity curve after burn-in period ends (or always if no burn-in).
                if (burnInEndDate is null || marketData.Key > burnInEndDate.Value)
                {
                    portfolio.UpdateEquityCurve(marketData.Key);
                }

                // Daily drawdown check — main portfolio only, not benchmark.
                // Runs after UpdateEquityCurve so current-day NAV is available.
                // Liquidation orders queue for next-bar execution (no look-ahead bias).
                if (portfolio == _portfolio && _drawdownControl is not null)
                {
                    await _drawdownControl.CheckAsync(portfolio, marketData.Key, cancellationToken).ConfigureAwait(false);
                }

                // Post-day callback — allows callers to flush order handler buffers
                // so risk evaluation uses same-day prices, not stale data from the next rebalance.
                if (portfolio == _portfolio && afterDayCallback is not null)
                {
                    await afterDayCallback(portfolio, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        _logger.LogInformation("Backtest complete: {DataPoints} equity curve points", _portfolio.EquityCurve.Count);

        // Calculate and analyze performance metrics for both the portfolio and the benchmark portfolio.
        return AnalyzePerformanceMetrics();
    }

    /// <summary>
    /// Analyzes the performance of the backtested portfolio and benchmark portfolio,
    /// calculating various performance metrics and generating a Tearsheet object.
    /// </summary>
    /// <returns>A Tearsheet object containing various performance metrics for the backtested portfolio and benchmark portfolio.</returns>
    public Tearsheet AnalyzePerformanceMetrics()
    {
        // D9 fix: Guard empty equity curve to prevent divide-by-zero
        if (_portfolio.EquityCurve.Count < 2)
        {
            throw new InvalidOperationException("Equity curve must contain at least 2 data points. Run the backtest first.");
        }

        // Use calendar-aware trading days per year when available
        var tdpy = _tradingCalendar?.TradingDaysPerYear ?? 252;

        // Calculate the required performance metrics for the entire portfolio
        var dailyReturns = _portfolio.EquityCurve.Values.ToArray().DailyReturns().ToArray();

        var annualizedReturn = dailyReturns.AnnualizedReturn(tdpy);
        var sharpeRatio = dailyReturns.AnnualizedSharpeRatio(_dailyRiskFreeRate, tdpy);
        var sortinoRatio = dailyReturns.AnnualizedSortinoRatio(_dailyRiskFreeRate, tdpy);
        var cagr = dailyReturns.CompoundAnnualGrowthRate(tdpy);
        var volatility = dailyReturns.AnnualizedVolatility(tdpy);

        var benchmarkDailyReturns = _benchmarkPortfolio.EquityCurve.Values.ToArray().DailyReturns().ToArray();
        var alpha = dailyReturns.Alpha(benchmarkDailyReturns, _dailyRiskFreeRate);
        var beta = dailyReturns.Beta(benchmarkDailyReturns);
        var informationRatio = dailyReturns.InformationRatio(benchmarkDailyReturns);

        var (drawdowns, maxDrawdown, maxDrawdownDuration) = _portfolio.EquityCurve.CalculateDrawdownsAndMaxDrawdownInfo();

        var calmarRatio = dailyReturns.CalmarRatio(tdpy);
        var omegaRatio = dailyReturns.OmegaRatio();
        var historicalVaR = dailyReturns.HistoricalVaR();
        var conditionalVaR = dailyReturns.ConditionalVaR();
        var skewness = dailyReturns.Skewness();
        var kurtosis = dailyReturns.Kurtosis();
        var winRate = dailyReturns.WinRate();
        var profitFactor = dailyReturns.ProfitFactor();
        var recoveryFactor = dailyReturns.RecoveryFactor();

        // Create a Tearsheet object for the entire portfolio
        return new Tearsheet(
            annualizedReturn,
            sharpeRatio,
            sortinoRatio,
            maxDrawdown,
            cagr,
            volatility,
            alpha,
            beta,
            informationRatio,
            _portfolio.EquityCurve,
            drawdowns,
            maxDrawdownDuration,
            calmarRatio,
            omegaRatio,
            historicalVaR,
            conditionalVaR,
            skewness,
            kurtosis,
            winRate,
            profitFactor,
            recoveryFactor
        );
    }

    /// <summary>
    /// Analyzes performance metrics for the benchmark portfolio.
    /// Uses the main portfolio as the reference for relative metrics (alpha, beta, information ratio).
    /// </summary>
    public Tearsheet AnalyzeBenchmarkPerformanceMetrics()
    {
        if (_benchmarkPortfolio.EquityCurve.Count < 2)
        {
            throw new InvalidOperationException("Benchmark equity curve must contain at least 2 data points. Run the backtest first.");
        }

        var tdpy = _tradingCalendar?.TradingDaysPerYear ?? 252;

        var dailyReturns = _benchmarkPortfolio.EquityCurve.Values.ToArray().DailyReturns().ToArray();

        var annualizedReturn = dailyReturns.AnnualizedReturn(tdpy);
        var sharpeRatio = dailyReturns.AnnualizedSharpeRatio(_dailyRiskFreeRate, tdpy);
        var sortinoRatio = dailyReturns.AnnualizedSortinoRatio(_dailyRiskFreeRate, tdpy);
        var cagr = dailyReturns.CompoundAnnualGrowthRate(tdpy);
        var volatility = dailyReturns.AnnualizedVolatility(tdpy);

        // Relative metrics: benchmark vs portfolio (reversed perspective)
        var portfolioDailyReturns = _portfolio.EquityCurve.Values.ToArray().DailyReturns().ToArray();
        var alpha = dailyReturns.Alpha(portfolioDailyReturns, _dailyRiskFreeRate);
        var beta = dailyReturns.Beta(portfolioDailyReturns);
        var informationRatio = dailyReturns.InformationRatio(portfolioDailyReturns);

        var (drawdowns, maxDrawdown, maxDrawdownDuration) = _benchmarkPortfolio.EquityCurve.CalculateDrawdownsAndMaxDrawdownInfo();

        var calmarRatio = dailyReturns.CalmarRatio(tdpy);
        var omegaRatio = dailyReturns.OmegaRatio();
        var historicalVaR = dailyReturns.HistoricalVaR();
        var conditionalVaR = dailyReturns.ConditionalVaR();
        var skewness = dailyReturns.Skewness();
        var kurtosis = dailyReturns.Kurtosis();
        var winRate = dailyReturns.WinRate();
        var profitFactor = dailyReturns.ProfitFactor();
        var recoveryFactor = dailyReturns.RecoveryFactor();

        return new Tearsheet(
            annualizedReturn,
            sharpeRatio,
            sortinoRatio,
            maxDrawdown,
            cagr,
            volatility,
            alpha,
            beta,
            informationRatio,
            _benchmarkPortfolio.EquityCurve,
            drawdowns,
            maxDrawdownDuration,
            calmarRatio,
            omegaRatio,
            historicalVaR,
            conditionalVaR,
            skewness,
            kurtosis,
            winRate,
            profitFactor,
            recoveryFactor
        );
    }

    /// <summary>
    /// Deducts daily expense ratios from each strategy's cash.
    /// Per-asset rates override the default rate. Fee is proportional to each position's value.
    /// Called once per trading day before UpdateEquityCurve.
    /// </summary>
    private void ApplyDailyExpenseDeduction(IPortfolio portfolio, DateOnly _, SortedDictionary<Domain.ValueObjects.Asset, MarketData> dayData)
    {
        foreach (var (_, strategy) in portfolio.Strategies)
        {
            // Per-position fee deducted from each asset's native currency
            foreach (var (asset, quantity) in strategy.Positions)
            {
                if (quantity <= 0)
                {
                    continue;
                }

                if (!dayData.TryGetValue(asset, out var marketData))
                {
                    continue;
                }

                var positionValue = quantity * marketData.AdjustedClose;

                var dailyRate = _perAssetDailyExpenseRates.TryGetValue(asset.Ticker, out var assetRate)
                    ? assetRate
                    : _defaultDailyExpenseRate;

                var positionFee = positionValue * dailyRate;
                if (positionFee > 0)
                {
                    // Deduct in the asset's native currency to avoid cross-currency mismatches
                    var assetCurrency = portfolio.AssetCurrencies.TryGetValue(asset, out var cur)
                        ? cur
                        : portfolio.BaseCurrency;
                    strategy.UpdateCash(assetCurrency, -positionFee);
                }
            }

            // Cash fee deducted per-currency bucket (ETF expense ratios apply to total NAV)
            foreach (var (currency, cashAmount) in strategy.Cash.ToList())
            {
                if (cashAmount > 0 && _defaultDailyExpenseRate > 0)
                {
                    var cashFee = cashAmount * _defaultDailyExpenseRate;
                    strategy.UpdateCash(currency, -cashFee);
                }
            }
        }
    }
}
