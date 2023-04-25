﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using Boutquin.Domain.Helpers;
using Boutquin.Trading.Domain.Extensions;
using Boutquin.Trading.Domain.Helpers;
using Boutquin.Trading.Domain.Interfaces;

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
    private readonly Portfolio _portfolio;

    /// <summary>
    /// The benchmark portfolio to use for the backtesting simulation,
    /// represented as a Portfolio object.
    /// </summary>
    private readonly Portfolio _benchmarkPortfolio;

    /// <summary>
    /// The market data source to use for loading historical market data
    /// and dividend data, represented as an IMarketDataSource object.
    /// </summary>
    private readonly IMarketDataSource _marketDataSource;

    /// <summary>
    /// Initializes a new instance of the BackTest class with a trading portfolio, benchmark portfolio, and market data source.
    /// </summary>
    /// <param name="portfolio">A Portfolio object representing the trading portfolio.</param>
    /// <param name="benchmarkPortfolio">A Portfolio object representing the benchmark portfolio.</param>
    /// <param name="marketDataSource">An object implementing the IMarketDataSource interface, responsible for providing market data for the backtest.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the provided arguments are null.</exception>
    public BackTest(Portfolio portfolio, Portfolio benchmarkPortfolio, IMarketDataSource marketDataSource)
    {
        _portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio), "The provided portfolio cannot be null.");
        _benchmarkPortfolio = benchmarkPortfolio ?? throw new ArgumentNullException(nameof(benchmarkPortfolio), "The provided benchmark portfolio cannot be null.");
        _marketDataSource = marketDataSource ?? throw new ArgumentNullException(nameof(marketDataSource), "The provided market data source cannot be null.");
    }

    /// <summary>
    /// Runs the backtest simulation for the specified start and end dates.
    /// </summary>
    /// <param name="startDate">A DateTime object representing the start date of the backtest simulation.</param>
    /// <param name="endDate">A DateTime object representing the end date of the backtest simulation.</param>
    /// <returns>A Tearsheet object containing various performance metrics for the backtested portfolio and benchmark portfolio.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided start date is greater than or equal to the end date.</exception>
    public Tearsheet Run(DateTime startDate, DateTime endDate)
    {
        // Retrieve the list of assets from the strategies
        var assets = new HashSet<string>();
        foreach (var strategy in _portfolio.Strategies)
        {
            assets.UnionWith(strategy.Assets);
        }

        // Add the benchmark asset to the list
        assets.UnionWith(_benchmarkPortfolio.Strategies.First().Assets);

        // Load historical market data and dividend data
        var historicalMarketData = _marketDataSource.LoadHistoricalMarketData(assets, startDate, endDate);
        var historicalDividendData = _marketDataSource.LoadHistoricalDividendData(assets, startDate, endDate);

        // Combine market and dividend data
        var events = new List<IEvent>(historicalMarketData.Values.Cast<IEvent>().Concat(historicalDividendData.Values));

        // Iterate through the combined list of events and handle them for both the main and benchmark portfolios
        foreach (var eventObj in events)
        {
            _portfolio.HandleEvent(eventObj);
            _portfolio.UpdateEquityCurve(eventObj.Timestamp);

            _benchmarkPortfolio.HandleEvent(eventObj);
            _benchmarkPortfolio.UpdateEquityCurve(eventObj.Timestamp);
        }

        // Calculate and analyze performance metrics
        return AnalyzePerformanceMetrics();
    }

    /// <summary>
    /// Analyzes the performance of the backtested portfolio and benchmark portfolio,
    /// calculating various performance metrics and generating a Tearsheet object.
    /// </summary>
    /// <returns>A Tearsheet object containing various performance metrics for the backtested portfolio and benchmark portfolio.</returns>
    public Tearsheet AnalyzePerformanceMetrics()
    {
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
            maxDrawdownDuration
        );
    }

    /// <summary>
    /// Adds a strategy to the backtested <see cref="Portfolio"/>.
    /// </summary>
    /// <param name="strategy">The strategy to be added to the <see cref="Portfolio"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="strategy"/> is null.</exception>
    public void AddStrategy(IStrategy strategy)
    {
        // Ensure that the strategy is not null.
        Guard.AgainstNull(() => strategy);

        _portfolio.AddStrategy(strategy);
    }
}
