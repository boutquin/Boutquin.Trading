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
    /// The base currency for the backtesting simulation.
    /// </summary>
    private readonly CurrencyCode _baseCurrency;

    private readonly ILogger<BackTest> _logger;

    private readonly decimal _dailyRiskFreeRate;

    private readonly IDrawdownControl? _drawdownControl;

    private readonly IBusinessCalendar? _tradingCalendar;

    private readonly decimal _defaultDailyExpenseRate;
    private readonly IReadOnlyDictionary<string, decimal> _perAssetDailyExpenseRates;

    /// <summary>
    /// Initializes a new instance of the BackTest class (backward-compatible overload).
    /// </summary>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, CurrencyCode baseCurrency)
        : this(portfolio, benchmarkPortfolio, baseCurrency, NullLogger<BackTest>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with structured logging.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency for the backtest.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the provided arguments are null.</exception>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, CurrencyCode baseCurrency, ILogger<BackTest> logger)
    {
        _portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio), "The provided portfolio cannot be null.");
        _benchmarkPortfolio = benchmarkPortfolio ?? throw new ArgumentNullException(nameof(benchmarkPortfolio), "The provided benchmark portfolio cannot be null.");
        _baseCurrency = baseCurrency;
        _logger = logger ?? NullLogger<BackTest>.Instance;
        _perAssetDailyExpenseRates = new Dictionary<string, decimal>();
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with structured logging and a risk-free rate.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency for the backtest.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <param name="dailyRiskFreeRate">The daily risk-free rate as a decimal (e.g., 0.05/252 for 5% annualized). Default: 0.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the provided arguments are null.</exception>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, CurrencyCode baseCurrency, ILogger<BackTest> logger, decimal dailyRiskFreeRate)
        : this(portfolio, benchmarkPortfolio, baseCurrency, logger)
    {
        _dailyRiskFreeRate = dailyRiskFreeRate;
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with drawdown control.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <param name="dailyRiskFreeRate">The daily risk-free rate as a decimal.</param>
    /// <param name="drawdownControl">Optional daily drawdown monitor and circuit breaker.</param>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, CurrencyCode baseCurrency, ILogger<BackTest> logger, decimal dailyRiskFreeRate, IDrawdownControl? drawdownControl)
        : this(portfolio, benchmarkPortfolio, baseCurrency, logger, dailyRiskFreeRate)
    {
        _drawdownControl = drawdownControl;
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with trading calendar support.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <param name="dailyRiskFreeRate">The daily risk-free rate as a decimal.</param>
    /// <param name="drawdownControl">Optional daily drawdown monitor and circuit breaker.</param>
    /// <param name="tradingCalendar">Optional trading calendar for non-trading-day filtering and market-aware annualization.</param>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, CurrencyCode baseCurrency, ILogger<BackTest> logger, decimal dailyRiskFreeRate, IDrawdownControl? drawdownControl, IBusinessCalendar? tradingCalendar)
        : this(portfolio, benchmarkPortfolio, baseCurrency, logger, dailyRiskFreeRate, drawdownControl)
    {
        _tradingCalendar = tradingCalendar;
    }

    /// <summary>
    /// Initializes a new instance of the BackTest class with expense ratio support.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="baseCurrency">A CurrencyCode enum value representing the base currency.</param>
    /// <param name="logger">A logger for structured logging.</param>
    /// <param name="dailyRiskFreeRate">The daily risk-free rate as a decimal.</param>
    /// <param name="drawdownControl">Optional daily drawdown monitor and circuit breaker.</param>
    /// <param name="tradingCalendar">Optional trading calendar for non-trading-day filtering.</param>
    /// <param name="annualExpenseRatioBps">Default annual expense ratio in basis points (e.g., 20 = 0.20%). Applied to assets without a per-asset override.</param>
    /// <param name="assetExpenseRatiosBps">Per-asset annual expense ratios in basis points, keyed by ticker. Overrides the default for specified assets.</param>
    public BackTest(IPortfolio portfolio, IPortfolio benchmarkPortfolio, CurrencyCode baseCurrency, ILogger<BackTest> logger, decimal dailyRiskFreeRate, IDrawdownControl? drawdownControl, IBusinessCalendar? tradingCalendar, decimal annualExpenseRatioBps, IReadOnlyDictionary<string, decimal>? assetExpenseRatiosBps = null)
        : this(portfolio, benchmarkPortfolio, baseCurrency, logger, dailyRiskFreeRate, drawdownControl, tradingCalendar)
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
    /// This overload is no longer supported because the IMarketDataFetcher interface has been removed.
    /// Use <see cref="RunAsync(Recipes.IBacktestDataset, CancellationToken, DateOnly?, Func{IPortfolio, CancellationToken, Task}?, bool)"/> instead.
    /// </summary>
    [Obsolete("Use the RunAsync(IBacktestDataset) overload. The old IMarketDataFetcher-based overload is no longer supported.")]
    public Task<Tearsheet> RunAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default, DateOnly? burnInEndDate = null, Func<IPortfolio, CancellationToken, Task>? afterDayCallback = null)
    {
        throw new NotSupportedException("Use the RunAsync(IBacktestDataset) overload. The old IMarketDataFetcher-based overload is no longer supported.");
    }

    /// <summary>
    /// Runs the backtest simulation from a pre-materialized dataset.
    /// No data fetching occurs — all data is provided by the <paramref name="dataset"/>.
    /// </summary>
    /// <param name="dataset">Pre-materialized backtest dataset from <see cref="Recipes.BacktestDatasetBuilder"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="burnInEndDate">Optional burn-in end date for equity curve tracking.</param>
    /// <param name="afterDayCallback">Optional callback invoked after each trading day.</param>
    /// <param name="ignoreDataQualityIssues">When <see langword="false"/> (default), the backtest
    /// aborts with <see cref="InvalidOperationException"/> if the dataset contains Warning or Error
    /// level data-quality issues (e.g., unexpected date gaps, fetch failures). Set to
    /// <see langword="true"/> to log the issues and proceed anyway.</param>
    /// <returns>A Tearsheet with performance metrics.</returns>
    public async Task<Tearsheet> RunAsync(
        Recipes.IBacktestDataset dataset,
        CancellationToken cancellationToken = default,
        DateOnly? burnInEndDate = null,
        Func<IPortfolio, CancellationToken, Task>? afterDayCallback = null,
        bool ignoreDataQualityIssues = false)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var startDate = dataset.Prices.Keys.FirstOrDefault();
        var endDate = dataset.Prices.Keys.LastOrDefault();

        if (startDate >= endDate)
        {
            throw new ArgumentException("Dataset must contain at least two distinct dates.");
        }

        if (burnInEndDate.HasValue && (burnInEndDate.Value <= startDate || burnInEndDate.Value >= endDate))
        {
            throw new ArgumentException(
                $"Burn-in end date ({burnInEndDate.Value}) must be strictly between {startDate} and {endDate}.",
                nameof(burnInEndDate));
        }

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Backtest starting from dataset: {StartDate} to {EndDate}", startDate, endDate);

        // Log data provenance so the user knows what sources the backtest runs on
        foreach (var prov in dataset.Provenance.DistinctBy(p => p.Dataset))
        {
            var dateTag = prov.DataDate.HasValue ? $" {prov.DataDate.Value:yyyy-MM-dd}" : "";
            _logger.LogInformation(
                "Data source: {Dataset} via {Provider} [{RetrievalMode}, {Freshness}{DateTag}]",
                prov.Dataset, prov.Provider, prov.RetrievalMode, prov.Freshness, dateTag);
        }

        // Surface data-quality issues from the pipeline.
        // "Info" issues (e.g., expected publication lags) are logged and allowed.
        // "Warning"/"Error" issues (e.g., unexpected date gaps, fetch failures)
        // abort the backtest unless ignoreDataQualityIssues is set.
        foreach (var issue in dataset.Issues)
        {
            if (issue.Severity is IssueSeverity.Error or IssueSeverity.Warning)
            {
                _logger.LogWarning("Data quality [{Code}]: {Message}", issue.Code, issue.Message);
            }
            else
            {
                _logger.LogInformation("Data [{Code}]: {Message}", issue.Code, issue.Message);
            }
        }

        var actionableIssues = dataset.Issues
            .Where(i => i.Severity is IssueSeverity.Warning or IssueSeverity.Error)
            .ToList();

        if (actionableIssues.Count > 0 && !ignoreDataQualityIssues)
        {
            var summary = string.Join("; ", actionableIssues.Select(i => $"[{i.Code}] {i.Message}"));
            throw new InvalidOperationException(
                $"Backtest aborted: {actionableIssues.Count} data quality issue(s). " +
                $"Set ignoreDataQualityIssues=true to override. Issues: {summary}");
        }

        // Event loop over pre-materialized data — no fetching
        foreach (var marketData in dataset.Prices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_tradingCalendar is not null && !_tradingCalendar.IsBusinessDay(marketData.Key))
            {
                _logger.LogWarning("Skipping non-trading day {Date}", marketData.Key);
                continue;
            }

            foreach (var portfolio in new[] { _portfolio, _benchmarkPortfolio })
            {
                await portfolio.ProcessPendingOrdersAsync(marketData.Key, marketData.Value, cancellationToken).ConfigureAwait(false);
            }

            var fxRates = dataset.FxRates.TryGetValue(marketData.Key, out var ratesForDate)
                          ? ratesForDate
                          : new SortedDictionary<CurrencyCode, decimal>();

            var marketEvent = new MarketEvent(marketData.Key, marketData.Value, fxRates);

            foreach (var portfolio in new[] { _portfolio, _benchmarkPortfolio })
            {
                await portfolio.HandleEventAsync(marketEvent, cancellationToken).ConfigureAwait(false);

                if (portfolio == _portfolio && (_defaultDailyExpenseRate > 0 || _perAssetDailyExpenseRates.Count > 0))
                {
                    ApplyDailyExpenseDeduction(portfolio, marketData.Key, marketData.Value);
                }

                if (burnInEndDate is null || marketData.Key > burnInEndDate.Value)
                {
                    portfolio.UpdateEquityCurve(marketData.Key);
                }

                if (portfolio == _portfolio && _drawdownControl is not null)
                {
                    await _drawdownControl.CheckAsync(portfolio, marketData.Key, cancellationToken).ConfigureAwait(false);
                }

                if (portfolio == _portfolio && afterDayCallback is not null)
                {
                    await afterDayCallback(portfolio, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        _logger.LogInformation("Backtest complete: {DataPoints} equity curve points", _portfolio.EquityCurve.Count);
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
        var tdpy = _tradingCalendar?.BusinessDaysPerYear ?? 252;

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

        var tdpy = _tradingCalendar?.BusinessDaysPerYear ?? 252;

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
    private void ApplyDailyExpenseDeduction(IPortfolio portfolio, DateOnly _, SortedDictionary<Symbol, Bar> dayData)
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
