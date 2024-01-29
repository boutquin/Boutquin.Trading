// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Domain.Extensions;

using Boutquin.Domain.Exceptions;
using Boutquin.Domain.Extensions;

using Exceptions;

using ExceptionMessages = Boutquin.Domain.Exceptions.ExceptionMessages;

/// <summary>
/// Provides extension methods for calculating trading performance metrics on an array of decimal values.
/// </summary>
public static class DecimalArrayExtensions
{
    private const int DefaultTradingDaysInYear = 252;

    /// <summary>
    /// Calculates the annualized return of a portfolio given an array of daily returns.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns for the portfolio.</param>
    /// <param name="tradingDaysPerYear">The number of trading days in a year.</param>
    /// <returns>The annualized return of the portfolio.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="NegativeTradingDaysPerYearException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    public static decimal AnnualizedReturn(
        this decimal[] dailyReturns, 
        int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input daily returns array is not null or empty.
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear);

        // Calculate the cumulative return of the portfolio.
        var cumulativeReturn = dailyReturns.Aggregate(1m, (acc, r) => acc * (r + 1m)) - 1m;

        // Calculate the annualized return of the portfolio.
        var annualizedReturn = (decimal)Math.Pow((double)(cumulativeReturn + 1m), (double)(tradingDaysPerYear / (decimal)dailyReturns.Length)) - 1m;

        // Return the annualized return.
        return annualizedReturn;
    }

    /// <summary>
    /// Calculates the daily volatility of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <returns>The daily volatility.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>
    public static decimal Volatility(this decimal[] dailyReturns)
    {
        // Ensure that the input daily returns array is not null or empty.
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);


        return dailyReturns.StandardDeviation();
    }

    /// <summary>
    /// Calculates the annualized volatility of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="tradingDaysPerYear">The number of trading days per year, by default 252.</param>
    /// <returns>The annualized volatility.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>
    /// <exception cref="NegativeTradingDaysPerYearException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    public static decimal AnnualizedVolatility(this decimal[] dailyReturns, int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear);

        // Relies on Volatility for remaining input validation.
        var dailyVolatility = dailyReturns.Volatility();
        return dailyVolatility * (decimal)Math.Sqrt(tradingDaysPerYear);
    }

    /// <summary>
    /// Calculates the Sharpe Ratio of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <returns>The Sharpe Ratio.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>
    public static decimal SharpeRatio(
        this decimal[] dailyReturns, 
        decimal riskFreeRate = 0m)
    {
        // Ensure that the input daily returns array is not null or empty.
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);


        var averageReturn = dailyReturns.Average() - riskFreeRate;
        var standardDeviation = dailyReturns.StandardDeviation();
        return averageReturn / standardDeviation;
    }

    /// <summary>
    /// Calculates the Annualized Sharpe Ratio of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <param name="tradingDaysPerYear">The number of trading days per year, by default 252.</param>
    /// <returns>The Annualized Sharpe Ratio.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>
    /// <exception cref="NegativeTradingDaysPerYearException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    public static decimal AnnualizedSharpeRatio(
        this decimal[] dailyReturns, 
        decimal riskFreeRate = 0m, 
        int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear);

        // Relies on SharpeRatio for remaining input validation.
        var sharpeRatio = dailyReturns.SharpeRatio(riskFreeRate);
        return sharpeRatio * (decimal)Math.Sqrt(tradingDaysPerYear);
    }

    /// <summary>
    /// Calculates the Sortino Ratio of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <returns>The Sortino Ratio.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>
    public static decimal SortinoRatio(
        this decimal[] dailyReturns, 
        decimal riskFreeRate = 0m)
    {
        // Ensure the array of daily returns is not null or empty
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var averageReturn = dailyReturns.Average() - riskFreeRate;
        var downsideDeviation = dailyReturns.DownsideDeviation(riskFreeRate);
        return averageReturn / downsideDeviation;
    }

    /// <summary>
    /// Calculates the Annualized Sortino Ratio of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <param name="tradingDaysPerYear">The number of trading days per year, by default 252.</param>
    /// <returns>The Annualized Sortino Ratio.</returns>
    /// <exception cref="NegativeTradingDaysPerYearException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>
    public static decimal AnnualizedSortinoRatio(
        this decimal[] dailyReturns, 
        decimal riskFreeRate = 0m, 
        int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear);

        // Relies on SortinoRatio for remaining input validation.
        var sortinoRatio = dailyReturns.SortinoRatio(riskFreeRate);
        return sortinoRatio * (decimal)Math.Sqrt(tradingDaysPerYear);
    }

    /// <summary>
    /// Calculates the Compound Annual Growth Rate (CAGR) of a strategy using an array of decimal values,
    /// representing the geometric average annual return of the strategy over the entire backtesting period,
    /// assuming the returns are reinvested.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="tradingDaysPerYear">The number of trading days per year, by default 252.</param>
    /// <returns>The CAGR expressed as a percentage.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>
    /// <exception cref="CalculationException">Thrown when the calculated CAGR value is too large or too small for a decimal.</exception>
    public static decimal CompoundAnnualGrowthRate(
        this decimal[] dailyReturns,
        double tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input daily returns array is not null or empty.
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear);

        // Calculate the cumulative return
        var cumulativeReturn = dailyReturns
            .Aggregate(1m, (current, dailyReturn) => current * (1 + dailyReturn));

        var totalTradingDays = dailyReturns.Length;
        var totalYears = totalTradingDays / tradingDaysPerYear;

        // Check if the totalYears is zero, and if so, throw an exception
        if (totalYears == 0)
        {
            throw new CalculationException("The total number of years must be greater than zero for CAGR calculation.");
        }

        try
        {
            // CAGR = [(Cumulative Return) ^ (1 / Total Years)] - 1
            var growthRate = (decimal)Math.Pow((double)cumulativeReturn, 1.0 / totalYears) - 1;
            return growthRate * 100;
        }
        catch (OverflowException ex)
        {
            throw new CalculationException("The calculated CAGR value is too large or too small for a decimal.", ex);
        }
    }


    /// <summary>
    /// Calculates the Downside Deviation of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <returns>The Downside Deviation.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>

    public static decimal DownsideDeviation(
        this decimal[] dailyReturns, 
        decimal riskFreeRate = 0m)
    {
        // Ensure the array of daily returns is not null or empty
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var downsideReturns = dailyReturns.Select(x => Math.Min(0, x - riskFreeRate)).ToArray();
        var squaredDownsideReturns = downsideReturns.Select(x => x * x).ToArray();
        var averageSquaredDownsideReturn = squaredDownsideReturns.Average();
        return (decimal)Math.Sqrt((double)averageSquaredDownsideReturn);
    }

    /// <summary>
    /// Calculates daily returns from an array of equity curve values.
    /// </summary>
    /// <param name="equityCurve">An array of equity curve values.</param>
    /// <returns>An array of daily returns.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="equityCurve"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="equityCurve"/> array contains less than two elements for sample calculation.</exception>
    public static decimal[] DailyReturns(
        this decimal[] equityCurve)
    {
        // Ensure that the input equity curve array is not null or empty.
        Guard.AgainstNullOrEmptyArray(() => equityCurve);
        // Check if there is enough data for sample calculation
        Guard.Against(equityCurve.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var dailyReturns = new decimal[equityCurve.Length - 1];

        // Calculate daily returns by iterating through the equity curve array
        for (var i = 1; i < equityCurve.Length; i++)
        {
            // Ensure that the previous equity value is not zero to prevent division by zero
            if (equityCurve[i - 1] == 0)
            {
                throw new CalculationException($"The equity curve contains a zero value at position {i - 1}, which leads to a division by zero in daily returns calculation.");
            }

            // Daily Return = (Current Equity Value / Previous Equity Value) - 1
            dailyReturns[i - 1] = (equityCurve[i] / equityCurve[i - 1]) - 1;
        }

        return dailyReturns;
    }

    /// <summary>
    /// Computes the equity curve from an array of daily returns.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns as decimal values.</param>
    /// <param name="initialInvestment">The initial investment value as a decimal.</param>
    /// <returns>An array representing the equity curve.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InvalidDailyReturnException">Thrown when an invalid daily return value is encountered.</exception>
    public static decimal[] EquityCurve(
        this decimal[] dailyReturns, 
        decimal initialInvestment = 10000m)
    {
        // Ensure the array of daily returns is not null or empty
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        
        var equityCurve = new decimal[dailyReturns.Length + 1];
        equityCurve[0] = initialInvestment;

        for (var i = 0; i < dailyReturns.Length; i++)
        {
            if (dailyReturns[i] < -1m)
            {
                throw new InvalidDailyReturnException($"Invalid daily return value at index {i}: {dailyReturns[i]}");
            }

            var growthFactor = 1m + dailyReturns[i];
            equityCurve[i + 1] = equityCurve[i] * growthFactor;
        }

        return equityCurve;
    }

    /// <summary>
    /// Calculates the Beta for a portfolio using daily returns, comparing the portfolio's performance to a benchmark index.
    /// </summary>
    /// <param name="portfolioDailyReturns">An array of daily returns for the portfolio.</param>
    /// <param name="benchmarkDailyReturns">An array of daily returns for the benchmark index.</param>
    /// <returns>The Beta value.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="portfolioDailyReturns"/> or <paramref name="benchmarkDailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="portfolioDailyReturns"/> or <paramref name="benchmarkDailyReturns"/> array contains less than two elements for sample calculation.</exception>
    public static decimal Beta(
        this decimal[] portfolioDailyReturns,
        decimal[] benchmarkDailyReturns)
    {
        // Ensure that the input daily returns arrays are not null or empty.
        Guard.AgainstNullOrEmptyArray(() => portfolioDailyReturns);
        Guard.AgainstNullOrEmptyArray(() => benchmarkDailyReturns);
        // Check if there is enough data for sample calculation
        Guard.Against(portfolioDailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);
        Guard.Against(benchmarkDailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var portfolioAverageReturn = portfolioDailyReturns.Average();
        var benchmarkAverageReturn = benchmarkDailyReturns.Average();

        // Calculate the covariance of the portfolio and benchmark returns
        var covariance = portfolioDailyReturns
            .Zip(benchmarkDailyReturns, (pReturn, bReturn) => (pReturn - portfolioAverageReturn) * (bReturn - benchmarkAverageReturn))
            .Sum() / (portfolioDailyReturns.Length - 1);

        // Calculate the variance of the benchmark returns
        var benchmarkVariance = benchmarkDailyReturns
            .Select(bReturn => (bReturn - benchmarkAverageReturn) * (bReturn - benchmarkAverageReturn))
            .Sum() / (benchmarkDailyReturns.Length - 1);

        // Beta = Covariance(Portfolio Returns, Benchmark Returns) / Variance(Benchmark Returns)
        var beta = covariance / benchmarkVariance;
        return beta;
    }

    /// <summary>
    /// Calculates the Alpha for a portfolio using daily returns, comparing the portfolio's performance to a benchmark index.
    /// </summary>
    /// <param name="portfolioDailyReturns">An array of daily returns for the portfolio.</param>
    /// <param name="benchmarkDailyReturns">An array of daily returns for the benchmark index.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <returns>The Alpha value.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="portfolioDailyReturns"/> or <paramref name="benchmarkDailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="portfolioDailyReturns"/> or <paramref name="benchmarkDailyReturns"/> array contains less than two elements for sample calculation.</exception>
    public static decimal Alpha(
        this decimal[] portfolioDailyReturns,
        decimal[] benchmarkDailyReturns,
        decimal riskFreeRate = 0m)
    {
        // Ensure that the input daily returns arrays are not null or empty.
        Guard.AgainstNullOrEmptyArray(() => portfolioDailyReturns);
        Guard.AgainstNullOrEmptyArray(() => benchmarkDailyReturns);
        // Check if there is enough data for sample calculation
        Guard.Against(portfolioDailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);
        Guard.Against(benchmarkDailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var portfolioAverageReturn = portfolioDailyReturns.Average();
        var benchmarkAverageReturn = benchmarkDailyReturns.Average();

        // Calculate the Beta
        var beta = Beta(portfolioDailyReturns, benchmarkDailyReturns);

        // Alpha = Portfolio Average Return - Risk Free Rate - Beta * (Benchmark Average Return - Risk Free Rate)
        var alpha = portfolioAverageReturn - riskFreeRate - beta * (benchmarkAverageReturn - riskFreeRate);
        return alpha;
    }

    /// <summary>
    /// Calculates the Information Ratio of daily returns for a given array of decimal values and their corresponding benchmark daily returns.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns for the portfolio.</param>
    /// <param name="benchmarkDailyReturns">An array of daily returns for the benchmark index.</param>
    /// <returns>The Information Ratio.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> or <paramref name="benchmarkDailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> or <paramref name="benchmarkDailyReturns"/> array contains less than two elements for sample calculation.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="dailyReturns"/> and <paramref name="benchmarkDailyReturns"/> arrays have different lengths.</exception>
    public static decimal InformationRatio(
        this decimal[] dailyReturns,
        decimal[] benchmarkDailyReturns)
    {
        // Ensure that the input daily returns array and benchmark daily returns array are not null or empty.
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        Guard.AgainstNullOrEmptyArray(() => benchmarkDailyReturns);

        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);
        Guard.Against(benchmarkDailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        // Ensure that the input daily returns array and benchmark daily returns array have the same length.
        if (dailyReturns.Length != benchmarkDailyReturns.Length)
        {
            throw new ArgumentException("The daily returns and benchmark daily returns arrays must have the same length.");
        }

        // Calculate the active returns, which is the difference between daily returns and benchmark daily returns.
        var activeReturns = dailyReturns.Zip(benchmarkDailyReturns, (portfolio, benchmark) => portfolio - benchmark).ToArray();

        // Calculate the average active return.
        var averageActiveReturn = activeReturns.Average();

        // Calculate the standard deviation of the active returns.
        var activeReturnStandardDeviation = activeReturns.StandardDeviation();

        // Calculate the Information Ratio.
        return averageActiveReturn / activeReturnStandardDeviation;
    }
}
