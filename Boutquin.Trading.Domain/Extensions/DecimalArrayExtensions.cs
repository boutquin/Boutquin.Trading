// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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

using Boutquin.Domain.Exceptions;
using Boutquin.Domain.Extensions;
using Boutquin.Trading.Domain.Exceptions;
using static Boutquin.Domain.Extensions.DecimalArrayExtensions;

namespace Boutquin.Trading.Domain.Extensions;

/// <summary>
/// Provides extension methods for calculating trading performance metrics on an array of decimal values.
/// </summary>
public static class DecimalArrayExtensions
{
    /// <summary>
    /// Calculates the Sharpe Ratio of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <returns>The Sharpe Ratio.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the input array is empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the input array contains less than two elements for sample calculation.</exception>
    public static decimal SharpeRatio(this decimal[] dailyReturns, decimal riskFreeRate = 0m)
    {
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
    /// <exception cref="EmptyOrNullArrayException">Thrown when the input array is empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the input array contains less than two elements for sample calculation.</exception>
    public static decimal AnnualizedSharpeRatio(this decimal[] dailyReturns, decimal riskFreeRate = 0m, int tradingDaysPerYear = 252)
    {
        var sharpeRatio = dailyReturns.SharpeRatio(riskFreeRate);
        return sharpeRatio * (decimal)Math.Sqrt(tradingDaysPerYear);
    }

    /// <summary>
    /// Calculates the Sortino Ratio of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <returns>The Sortino Ratio.</returns>
    public static decimal SortinoRatio(this decimal[] dailyReturns, decimal riskFreeRate = 0m)
    {
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
    public static decimal AnnualizedSortinoRatio(this decimal[] dailyReturns, decimal riskFreeRate = 0m, int tradingDaysPerYear = 252)
    {
        var sortinoRatio = dailyReturns.SortinoRatio(riskFreeRate);
        return sortinoRatio * (decimal)Math.Sqrt(tradingDaysPerYear);
    }

    /// <summary>
    /// Calculates the Downside Deviation of daily returns for a given array of decimal values.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns.</param>
    /// <param name="riskFreeRate">The risk-free rate, expressed as a daily value.</param>
    /// <returns>The Downside Deviation.</returns>
    public static decimal DownsideDeviation(this decimal[] dailyReturns, decimal riskFreeRate = 0m)
    {
        if (dailyReturns == null || dailyReturns.Length == 0)
        {
            throw new EmptyOrNullArrayException();
        }

        if (dailyReturns.Length == 1)
        {
            throw new InsufficientDataException(ExceptionMessages.InsufficientDataForSampleCalculation);
        }

        var downsideReturns = dailyReturns.Select(x => Math.Min(0, x - riskFreeRate)).ToArray();
        var squaredDownsideReturns = downsideReturns.Select(x => x * x).ToArray();
        var averageSquaredDownsideReturn = squaredDownsideReturns.Average();
        return (decimal)Math.Sqrt((double)averageSquaredDownsideReturn);
    }

    /// <summary>
    /// Computes the equity curve from an array of daily returns.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns as decimal values.</param>
    /// <param name="initialInvestment">The initial investment value as a decimal.</param>
    /// <returns>An array representing the equity curve.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when the input array is empty.</exception>
    /// <exception cref="InvalidDailyReturnException">Thrown when an invalid daily return value is encountered.</exception>
    public static decimal[] EquityCurve(this decimal[] dailyReturns, decimal initialInvestment = 10000m)
    {
        if (dailyReturns == null || dailyReturns.Length == 0)
        {
            throw new EmptyOrNullArrayException();
        }

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
}
