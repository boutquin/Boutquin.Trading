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
    }

    /// <summary>
    /// Runs the backtest simulation asynchronously for the specified start and end dates.
    /// </summary>
    /// <param name="startDate">A DateOnly object representing the start date of the backtest simulation.</param>
    /// <param name="endDate">A DateOnly object representing the end date of the backtest simulation.</param>
    /// <returns>A Task that represents the asynchronous operation. The task result contains a Tearsheet object containing various performance metrics for the backtested portfolio and benchmark portfolio.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided start date is greater than or equal to the end date.</exception>
    public async Task<Tearsheet> RunAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        // Validate the start and end dates.
        if (startDate >= endDate)
        {
            throw new ArgumentException("The start date must be earlier than the end date.", nameof(startDate));
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Backtest starting: {StartDate} to {EndDate}", startDate, endDate);

        // Fetch the historical market data for the backtest period for both the portfolio and the benchmark portfolio.
        // L1: .Union() already deduplicates — removed redundant .Distinct()
        var symbols = _portfolio.Strategies.Values.SelectMany(s => s.Assets.Keys)
                      .Union(_benchmarkPortfolio.Strategies.Values.SelectMany(s => s.Assets.Keys));

        var marketDataTimeline = _marketDataFetcher.FetchMarketDataAsync(symbols, cancellationToken);

        // H3: Include both portfolio AND benchmark asset currencies for FX rate fetching
        var currencyPairs = _portfolio.Strategies.Values
                                      .Concat(_benchmarkPortfolio.Strategies.Values)
                                      .SelectMany(s => s.Assets.Values)
                                      .Select(currencyCode => $"{_baseCurrency}_{currencyCode}")
                                      .Distinct();

        // Fetch the historical FX rates for the currency pairs.
        var fxRatesTimeline = _marketDataFetcher.FetchFxRatesAsync(currencyPairs, cancellationToken);

        // Create a dictionary to hold the FX rates for each date.
        var fxRatesForDate = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();

        // Fill the dictionary with FX rates for each date.
        await foreach (var fxRatesOnDate in fxRatesTimeline.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            fxRatesForDate[fxRatesOnDate.Key] = fxRatesOnDate.Value;
        }

        // M32: Warn when no FX conversion rates are loaded
        if (fxRatesForDate.Count == 0)
        {
            _logger.LogWarning("No FX conversion rates loaded. Foreign currency assets may fail valuation.");
        }

        // Iterate through the market data timeline and handle each event.
        await foreach (var marketData in marketDataTimeline.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // BUG-A07: Filter market data to startDate..endDate range
            if (marketData.Key < startDate || marketData.Key > endDate)
            {
                continue;
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
            foreach (var portfolio in new[] { _portfolio, _benchmarkPortfolio })
            {
                await portfolio.HandleEventAsync(marketEvent, cancellationToken).ConfigureAwait(false);
                portfolio.UpdateEquityCurve(marketData.Key);
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

        // Calculate the required performance metrics for the entire portfolio
        var dailyReturns = _portfolio.EquityCurve.Values.ToArray().DailyReturns().ToArray();

        var annualizedReturn = dailyReturns.AnnualizedReturn();
        var sharpeRatio = dailyReturns.SharpeRatio();
        var sortinoRatio = dailyReturns.SortinoRatio();
        var cagr = dailyReturns.CompoundAnnualGrowthRate();
        var volatility = dailyReturns.Volatility();

        var benchmarkDailyReturns = _benchmarkPortfolio.EquityCurve.Values.ToArray().DailyReturns().ToArray();
        var alpha = dailyReturns.Alpha(benchmarkDailyReturns);
        var beta = dailyReturns.Beta(benchmarkDailyReturns);
        var informationRatio = dailyReturns.InformationRatio(benchmarkDailyReturns);

        var (drawdowns, maxDrawdown, maxDrawdownDuration) = _portfolio.EquityCurve.CalculateDrawdownsAndMaxDrawdownInfo();

        var calmarRatio = dailyReturns.CalmarRatio();
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
}
