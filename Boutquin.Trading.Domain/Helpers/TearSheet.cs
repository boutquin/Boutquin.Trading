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

namespace Boutquin.Trading.Domain.Helpers;

/// <summary>
/// The Tearsheet record encapsulates the performance metrics and statistics
/// for a backtested trading strategy, providing an overview of the strategy's
/// risk and return characteristics.
/// The Tearsheet record includes multiple performance indicators such as
/// annualized return, Sharpe ratio, Sortino ratio, maximum drawdown, compound
/// annual growth rate (CAGR), volatility, alpha, beta, information ratio,
/// equity curve, drawdowns, and maximum drawdown duration.
/// </summary>
/// <param name="AnnualizedReturn">The annualized return of the strategy,
/// expressed as a percentage. It represents the average yearly return of
/// the strategy over the entire backtesting period.
/// </param>
/// <param name="SharpeRatio">The Sharpe ratio of the strategy, a widely
/// used risk-adjusted performance measure. It is calculated by dividing
/// the strategy's excess return (return above the risk-free rate) by its
/// volatility (standard deviation of returns).
/// </param>
/// <param name="SortinoRatio">The Sortino ratio of the strategy, a
/// risk-adjusted performance measure similar to the Sharpe ratio,
/// but it only considers the downside volatility. It is calculated by
/// dividing the strategy's excess return (return above the risk-free rate)
/// by its downside deviation (standard deviation of negative returns).
/// </param>
/// <param name="MaxDrawdown">The maximum drawdown of the strategy, expressed
/// as a percentage. It represents the largest peak-to-trough decline in the
/// value of the strategy during the backtesting period.
/// </param>
/// <param name="CAGR">The compound annual growth rate
/// (CAGR) of the strategy,
/// expressed as a percentage. It represents the geometric average annual
/// return of the strategy over the entire backtesting period, assuming
/// the returns are reinvested.
/// </param>
/// <param name="Volatility">The volatility of the strategy, expressed as
/// a percentage. It represents the annualized standard deviation of the
/// strategy's returns and measures the degree of fluctuation in the
/// strategy's value over time.
/// </param>
/// <param name="Alpha">The alpha of the strategy, a risk-adjusted
/// performance measure that represents the strategy's excess return
/// relative to a benchmark index or another reference portfolio,
/// after accounting for the strategy's beta (market risk).
/// </param>
/// <param name="Beta">The beta of the strategy, a measure of the
/// strategy's sensitivity to market movements. A beta of 1 indicates
/// that the strategy's returns move in line with the market, while
/// a beta greater (less) than 1 indicates that the strategy is
/// more (less) volatile than the market.
/// </param>
/// <param name="InformationRatio">The information ratio of the
/// strategy, a risk-adjusted performance measure that compares the
/// strategy's excess return (return above a benchmark index) to its
/// tracking error (standard deviation of the strategy's excess returns).
/// A higher information ratio indicates better risk-adjusted performance.
/// </param>
/// <param name="EquityCurve">The equity curve of the strategy,
/// represented as a dictionary with DateTime keys and equity values.
/// The equity curve illustrates the growth of the strategy's value over
/// time, providing a visual representation of the strategy's performance
/// during the backtesting period.
/// </param>
/// <param name="Drawdowns">The drawdowns of the strategy, represented
/// as a dictionary with DateTime keys and drawdown values. Each entry
/// in the dictionary represents a peak-to-trough decline in the value
/// of the strategy during the backtesting period.
/// </param>
/// <param name="MaxDrawdownDuration">The maximum drawdown duration
/// of the strategy, in days. It represents the longest period
/// between the peak and the subsequent recovery to a new peak
/// in the strategy's value during the backtesting period.
/// </param>
public record Tearsheet(
    decimal AnnualizedReturn,
    decimal SharpeRatio,
    decimal SortinoRatio,
    decimal MaxDrawdown,
    // ReSharper disable once InconsistentNaming
    decimal CAGR,
    decimal Volatility,
    decimal Alpha,
    decimal Beta,
    decimal InformationRatio,
    SortedDictionary<DateOnly, decimal> EquityCurve,
    SortedDictionary<DateOnly, decimal> Drawdowns,
    int MaxDrawdownDuration)
{
    /// <summary>
    /// Returns a string representation of the tearsheet, displaying key performance metrics and statistics.
    /// </summary>
    /// <returns>A string representation of the tearsheet.</returns>
    public override string ToString()
    {
        return $"Annualized Return: {AnnualizedReturn:P2}\n" +
               $"Sharpe Ratio: {SharpeRatio:F2}\n" +
               $"Sortino Ratio: {SortinoRatio:F2}\n" +
               $"Max Drawdown: {MaxDrawdown:P2}\n" +
               $"CAGR: {CAGR:P2}\n" +
               $"Volatility: {Volatility:P2}\n" +
               $"Alpha: {Alpha:F2}\n" +
               $"Beta: {Beta:F2}\n" +
               $"Information Ratio: {InformationRatio:F2}\n" +
               $"Max Drawdown Duration: {MaxDrawdownDuration} days";
    }
}
