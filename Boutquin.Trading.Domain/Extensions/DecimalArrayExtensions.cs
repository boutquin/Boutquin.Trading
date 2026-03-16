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

namespace Boutquin.Trading.Domain.Extensions;

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    public static decimal AnnualizedReturn(
        this decimal[] dailyReturns,
        int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input daily returns array is not null or empty.
        Guard.AgainstNullOrEmptyArray(() => dailyReturns); // Throws EmptyOrNullArrayException
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear); // Throws ArgumentOutOfRangeException

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
        Guard.AgainstNullOrEmptyArray(() => dailyReturns); // Throws EmptyOrNullArrayException
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    public static decimal AnnualizedVolatility(this decimal[] dailyReturns, int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear); // Throws ArgumentOutOfRangeException

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
        Guard.AgainstNullOrEmptyArray(() => dailyReturns); // Throws EmptyOrNullArrayException
        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var averageReturn = dailyReturns.Average() - riskFreeRate;
        var standardDeviation = dailyReturns.StandardDeviation();

        // B5 fix: Guard zero standard deviation to prevent division by zero
        if (standardDeviation == 0m)
        {
            throw new CalculationException("Standard deviation is zero; cannot compute Sharpe ratio.");
        }

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    public static decimal AnnualizedSharpeRatio(
        this decimal[] dailyReturns,
        decimal riskFreeRate = 0m,
        int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear); // Throws ArgumentOutOfRangeException

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
        Guard.AgainstNullOrEmptyArray(() => dailyReturns); // Throws EmptyOrNullArrayException
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="dailyReturns"/> array contains less than two elements for sample calculation.</exception>
    public static decimal AnnualizedSortinoRatio(
        this decimal[] dailyReturns,
        decimal riskFreeRate = 0m,
        int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear); // Throws ArgumentOutOfRangeException

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
    /// <exception cref ="ArgumentOutOfRangeException">Thrown when the <paramref name="tradingDaysPerYear"/> is non-positive.</exception>
    public static decimal CompoundAnnualGrowthRate(
        this decimal[] dailyReturns,
        int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        // Ensure that the input daily returns array is not null or empty.
        Guard.AgainstNullOrEmptyArray(() => dailyReturns); // Throws EmptyOrNullArrayException
        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);
        // Ensure that the input trading days per year is positive.
        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear); // Throws ArgumentOutOfRangeException

        // Calculate the cumulative return
        var cumulativeReturn = dailyReturns
            .Aggregate(1m, (current, dailyReturn) => current * (1 + dailyReturn));

        var totalTradingDays = dailyReturns.Length;
        var totalYears = (double)totalTradingDays / tradingDaysPerYear;

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
        Guard.AgainstNullOrEmptyArray(() => dailyReturns); // Throws EmptyOrNullArrayException
        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var downsideReturns = dailyReturns.Select(x => Math.Min(0, x - riskFreeRate)).ToArray();
        var squaredDownsideReturns = downsideReturns.Select(x => x * x).ToArray();
        // B2 fix: Use sample divisor (N-1) to align with StandardDeviation (sample-based)
        var averageSquaredDownsideReturn = squaredDownsideReturns.Sum() / (squaredDownsideReturns.Length - 1);
        var result = (decimal)Math.Sqrt((double)averageSquaredDownsideReturn);

        // B5 fix: Guard zero downside deviation to prevent division by zero in SortinoRatio
        if (result == 0m)
        {
            throw new CalculationException("Downside deviation is zero; cannot compute Sortino ratio.");
        }

        return result;
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
        Guard.AgainstNullOrEmptyArray(() => equityCurve); // Throws EmptyOrNullArrayException
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
        Guard.AgainstNullOrEmptyArray(() => dailyReturns); // Throws EmptyOrNullArrayException

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
        Guard.AgainstNullOrEmptyArray(() => portfolioDailyReturns); // Throws EmptyOrNullArrayException
        Guard.AgainstNullOrEmptyArray(() => benchmarkDailyReturns); // Throws EmptyOrNullArrayException
        // Check if there is enough data for sample calculation
        Guard.Against(portfolioDailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);
        Guard.Against(benchmarkDailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        // B3 fix: Guard mismatched array lengths
        if (portfolioDailyReturns.Length != benchmarkDailyReturns.Length)
        {
            throw new ArgumentException("Portfolio and benchmark daily returns arrays must have the same length.", nameof(benchmarkDailyReturns));
        }

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

        // B5 fix: Guard zero benchmark variance to prevent division by zero
        if (benchmarkVariance == 0m)
        {
            throw new CalculationException("Benchmark variance is zero; cannot compute Beta.");
        }

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
        Guard.AgainstNullOrEmptyArray(() => portfolioDailyReturns); // Throws EmptyOrNullArrayException
        Guard.AgainstNullOrEmptyArray(() => benchmarkDailyReturns); // Throws EmptyOrNullArrayException
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
        Guard.AgainstNullOrEmptyArray(() => dailyReturns); // Throws EmptyOrNullArrayException
        Guard.AgainstNullOrEmptyArray(() => benchmarkDailyReturns); // Throws EmptyOrNullArrayException

        // Check if there is enough data for sample calculation
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);
        Guard.Against(benchmarkDailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        // Ensure that the input daily returns array and benchmark daily returns array have the same length.
        if (dailyReturns.Length != benchmarkDailyReturns.Length)
        {
            throw new ArgumentException("The daily returns and benchmark daily returns arrays must have the same length.", nameof(benchmarkDailyReturns));
        }

        // Calculate the active returns, which is the difference between daily returns and benchmark daily returns.
        var activeReturns = dailyReturns.Zip(benchmarkDailyReturns, (portfolio, benchmark) => portfolio - benchmark).ToArray();

        // Calculate the average active return.
        var averageActiveReturn = activeReturns.Average();

        // Calculate the standard deviation of the active returns.
        var activeReturnStandardDeviation = activeReturns.StandardDeviation();

        // B5 fix: Guard zero active return standard deviation to prevent division by zero
        if (activeReturnStandardDeviation == 0m)
        {
            throw new CalculationException("Active return standard deviation is zero; cannot compute Information Ratio.");
        }

        // Calculate the Information Ratio.
        return averageActiveReturn / activeReturnStandardDeviation;
    }

    /// <summary>
    /// Calculates the Calmar Ratio: CAGR divided by the absolute value of the maximum drawdown.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="tradingDaysPerYear">The number of trading days per year, by default 252.</param>
    /// <returns>The Calmar Ratio.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the array contains fewer than two elements.</exception>
    /// <exception cref="CalculationException">Thrown when the maximum drawdown is zero (no drawdown occurred).</exception>
    public static decimal CalmarRatio(
        this decimal[] dailyReturns,
        int tradingDaysPerYear = DefaultTradingDaysInYear)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var cagr = dailyReturns.CompoundAnnualGrowthRate(tradingDaysPerYear);
        var equityCurve = dailyReturns.EquityCurve();
        var maxDrawdown = MaxDrawdownFromEquityCurve(equityCurve);

        if (maxDrawdown == 0m)
        {
            throw new CalculationException("Maximum drawdown is zero; cannot compute Calmar Ratio.");
        }

        return cagr / Math.Abs(maxDrawdown);
    }

    /// <summary>
    /// Calculates the Omega Ratio: the ratio of gains above a threshold to losses below it.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="threshold">The threshold return (default 0).</param>
    /// <returns>The Omega Ratio.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="CalculationException">Thrown when there are no returns below the threshold.</exception>
    public static decimal OmegaRatio(
        this decimal[] dailyReturns,
        decimal threshold = 0m)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);

        var gains = dailyReturns.Sum(r => Math.Max(r - threshold, 0m));
        var losses = dailyReturns.Sum(r => Math.Max(threshold - r, 0m));

        if (losses == 0m)
        {
            throw new CalculationException("Sum of losses below threshold is zero; cannot compute Omega Ratio.");
        }

        return gains / losses;
    }

    /// <summary>
    /// Calculates the Historical Value at Risk (VaR) at a given confidence level using the percentile method.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="confidenceLevel">The confidence level (e.g., 0.95 for 95%).</param>
    /// <returns>The VaR as a negative number representing the loss threshold.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    public static decimal HistoricalVaR(
        this decimal[] dailyReturns,
        decimal confidenceLevel = 0.95m)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);

        var sorted = dailyReturns.OrderBy(r => r).ToArray();
        var index = (double)(1m - confidenceLevel) * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        var fraction = (decimal)(index - lower);

        return sorted[lower] + fraction * (sorted[upper] - sorted[lower]);
    }

    /// <summary>
    /// Calculates the Parametric Value at Risk (VaR) assuming normally distributed returns.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="confidenceLevel">The confidence level (e.g., 0.95 for 95%).</param>
    /// <returns>The VaR as a negative number representing the loss threshold.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the array contains fewer than two elements.</exception>
    public static decimal ParametricVaR(
        this decimal[] dailyReturns,
        decimal confidenceLevel = 0.95m)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var mean = dailyReturns.Average();
        var stdDev = dailyReturns.StandardDeviation();
        var zScore = (decimal)NormalInverseCdf((double)confidenceLevel);

        return mean - zScore * stdDev;
    }

    /// <summary>
    /// Calculates the Conditional Value at Risk (CVaR), also known as Expected Shortfall.
    /// This is the expected loss given that the loss exceeds the VaR threshold.
    /// CVaR is always less than or equal to VaR (more negative = worse).
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="confidenceLevel">The confidence level (e.g., 0.95 for 95%).</param>
    /// <returns>The CVaR value.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="CalculationException">Thrown when no returns fall at or below the VaR threshold.</exception>
    public static decimal ConditionalVaR(
        this decimal[] dailyReturns,
        decimal confidenceLevel = 0.95m)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);

        var var = dailyReturns.HistoricalVaR(confidenceLevel);
        var tailReturns = dailyReturns.Where(r => r <= var).ToArray();

        if (tailReturns.Length == 0)
        {
            throw new CalculationException("No returns at or below VaR threshold; cannot compute CVaR.");
        }

        return tailReturns.Average();
    }

    /// <summary>
    /// Calculates the sample skewness of the return distribution.
    /// Positive skew indicates a longer right tail; negative skew indicates a longer left tail.
    /// Uses the adjusted Fisher-Pearson standardized moment coefficient.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <returns>The sample skewness.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the array contains fewer than three elements.</exception>
    /// <exception cref="CalculationException">Thrown when the standard deviation is zero.</exception>
    public static decimal Skewness(this decimal[] dailyReturns)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        if (dailyReturns.Length < 3)
        {
            throw new InsufficientDataException("At least three data points are required to compute skewness.");
        }

        var n = dailyReturns.Length;
        var mean = dailyReturns.Average();
        var stdDev = dailyReturns.StandardDeviation();

        if (stdDev == 0m)
        {
            throw new CalculationException("Standard deviation is zero; cannot compute skewness.");
        }

        var sumCubed = dailyReturns.Sum(r =>
        {
            var deviation = (r - mean) / stdDev;
            return deviation * deviation * deviation;
        });

        // Adjusted Fisher-Pearson: n / ((n-1)(n-2)) * Σ((xi - mean)/s)^3
        return (decimal)n / ((n - 1) * (n - 2)) * sumCubed;
    }

    /// <summary>
    /// Calculates the excess kurtosis (Fisher) of the return distribution.
    /// A value of 0 indicates a normal distribution; positive indicates heavier tails.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <returns>The excess kurtosis.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the array contains fewer than four elements.</exception>
    /// <exception cref="CalculationException">Thrown when the standard deviation is zero.</exception>
    public static decimal Kurtosis(this decimal[] dailyReturns)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        if (dailyReturns.Length < 4)
        {
            throw new InsufficientDataException("At least four data points are required to compute kurtosis.");
        }

        var n = dailyReturns.Length;
        var mean = dailyReturns.Average();
        var stdDev = dailyReturns.StandardDeviation();

        if (stdDev == 0m)
        {
            throw new CalculationException("Standard deviation is zero; cannot compute kurtosis.");
        }

        var sumFourth = dailyReturns.Sum(r =>
        {
            var deviation = (r - mean) / stdDev;
            var squared = deviation * deviation;
            return squared * squared;
        });

        // Sample excess kurtosis formula
        var term1 = (decimal)(n * (n + 1)) / ((n - 1) * (n - 2) * (n - 3)) * sumFourth;
        var term2 = 3m * (decimal)((n - 1) * (n - 1)) / ((n - 2) * (n - 3));

        return term1 - term2;
    }

    /// <summary>
    /// Calculates the win rate: the proportion of positive daily returns.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <returns>A value between 0 and 1 representing the fraction of winning days.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    public static decimal WinRate(this decimal[] dailyReturns)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);

        return (decimal)dailyReturns.Count(r => r > 0m) / dailyReturns.Length;
    }

    /// <summary>
    /// Calculates the Profit Factor: the ratio of gross profits to gross losses.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <returns>The Profit Factor.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="CalculationException">Thrown when there are no negative returns (no losses to divide by).</exception>
    public static decimal ProfitFactor(this decimal[] dailyReturns)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);

        var grossProfit = dailyReturns.Where(r => r > 0m).Sum();
        var grossLoss = Math.Abs(dailyReturns.Where(r => r < 0m).Sum());

        if (grossLoss == 0m)
        {
            throw new CalculationException("Gross loss is zero; cannot compute Profit Factor.");
        }

        return grossProfit / grossLoss;
    }

    /// <summary>
    /// Calculates the Recovery Factor: cumulative return divided by the absolute maximum drawdown.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <returns>The Recovery Factor.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the <paramref name="dailyReturns"/> array is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the array contains fewer than two elements.</exception>
    /// <exception cref="CalculationException">Thrown when the maximum drawdown is zero.</exception>
    public static decimal RecoveryFactor(this decimal[] dailyReturns)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);
        Guard.Against(dailyReturns.Length == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        var cumulativeReturn = dailyReturns.Aggregate(1m, (acc, r) => acc * (r + 1m)) - 1m;
        var equityCurve = dailyReturns.EquityCurve();
        var maxDrawdown = MaxDrawdownFromEquityCurve(equityCurve);

        if (maxDrawdown == 0m)
        {
            throw new CalculationException("Maximum drawdown is zero; cannot compute Recovery Factor.");
        }

        return cumulativeReturn / Math.Abs(maxDrawdown);
    }

    /// <summary>
    /// Computes the maximum drawdown from an equity curve array (not date-indexed).
    /// </summary>
    private static decimal MaxDrawdownFromEquityCurve(decimal[] equityCurve)
    {
        var peak = equityCurve[0];
        var maxDrawdown = 0m;

        foreach (var value in equityCurve)
        {
            if (value > peak)
            {
                peak = value;
            }

            if (peak > 0m)
            {
                var drawdown = (value - peak) / peak;
                if (drawdown < maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        return maxDrawdown;
    }

    /// <summary>
    /// Approximation of the inverse CDF (quantile function) for the standard normal distribution.
    /// Uses the rational approximation from Abramowitz and Stegun.
    /// </summary>
    private static double NormalInverseCdf(double p)
    {
        // Coefficients for the rational approximation
        const double a1 = -3.969683028665376e+01;
        const double a2 = 2.209460984245205e+02;
        const double a3 = -2.759285104469687e+02;
        const double a4 = 1.383577518672690e+02;
        const double a5 = -3.066479806614716e+01;
        const double a6 = 2.506628277459239e+00;

        const double b1 = -5.447609879822406e+01;
        const double b2 = 1.615858368580409e+02;
        const double b3 = -1.556989798598866e+02;
        const double b4 = 6.680131188771972e+01;
        const double b5 = -1.328068155288572e+01;

        const double c1 = -7.784894002430293e-03;
        const double c2 = -3.223964580411365e-01;
        const double c3 = -2.400758277161838e+00;
        const double c4 = -2.549732539343734e+00;
        const double c5 = 4.374664141464968e+00;
        const double c6 = 2.938163982698783e+00;

        const double d1 = 7.784695709041462e-03;
        const double d2 = 3.224671290700398e-01;
        const double d3 = 2.445134137142996e+00;
        const double d4 = 3.754408661907416e+00;

        const double pLow = 0.02425;
        const double pHigh = 1 - pLow;

        double q, r;

        if (p < pLow)
        {
            q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6) /
                   ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
        }

        if (p <= pHigh)
        {
            q = p - 0.5;
            r = q * q;
            return (((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6) * q /
                   (((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1);
        }

        q = Math.Sqrt(-2 * Math.Log(1 - p));
        return -(((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6) /
               ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
    }
}
