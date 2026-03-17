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

namespace Boutquin.Trading.Domain.Helpers;

/// <summary>
/// The Tearsheet record encapsulates the performance metrics and statistics
/// for a backtested trading strategy, providing an overview of the strategy's
/// risk and return characteristics.
/// </summary>
/// <param name="AnnualizedReturn">The annualized return of the strategy, expressed as a percentage.</param>
/// <param name="SharpeRatio">The Sharpe ratio of the strategy.</param>
/// <param name="SortinoRatio">The Sortino ratio of the strategy.</param>
/// <param name="MaxDrawdown">The maximum drawdown of the strategy, expressed as a percentage.</param>
/// <param name="CAGR">The compound annual growth rate (CAGR) of the strategy.</param>
/// <param name="Volatility">The annualized volatility of the strategy.</param>
/// <param name="Alpha">The alpha of the strategy relative to a benchmark.</param>
/// <param name="Beta">The beta of the strategy relative to a benchmark.</param>
/// <param name="InformationRatio">The information ratio of the strategy.</param>
/// <param name="EquityCurve">The equity curve of the strategy over time.</param>
/// <param name="Drawdowns">The drawdowns of the strategy over time.</param>
/// <param name="MaxDrawdownDuration">The maximum drawdown duration in days.</param>
/// <param name="CalmarRatio">The Calmar Ratio: CAGR / |MaxDrawdown|.</param>
/// <param name="OmegaRatio">The Omega Ratio: gains above threshold / losses below threshold.</param>
/// <param name="HistoricalVaR">The Historical Value at Risk at 95% confidence.</param>
/// <param name="ConditionalVaR">The Conditional VaR (Expected Shortfall) at 95% confidence.</param>
/// <param name="Skewness">The sample skewness of the return distribution.</param>
/// <param name="Kurtosis">The excess kurtosis of the return distribution.</param>
/// <param name="WinRate">The proportion of positive return days (0-1).</param>
/// <param name="ProfitFactor">The ratio of gross profits to gross losses.</param>
/// <param name="RecoveryFactor">The cumulative return divided by |MaxDrawdown|.</param>
public sealed record Tearsheet(
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
    int MaxDrawdownDuration,
    decimal CalmarRatio,
    decimal OmegaRatio,
    decimal HistoricalVaR,
    decimal ConditionalVaR,
    decimal Skewness,
    decimal Kurtosis,
    decimal WinRate,
    decimal ProfitFactor,
    decimal RecoveryFactor)
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
               $"Calmar Ratio: {CalmarRatio:F2}\n" +
               $"Omega Ratio: {OmegaRatio:F2}\n" +
               $"Historical VaR (95%): {HistoricalVaR:P2}\n" +
               $"Conditional VaR (95%): {ConditionalVaR:P2}\n" +
               $"Skewness: {Skewness:F4}\n" +
               $"Kurtosis: {Kurtosis:F4}\n" +
               $"Win Rate: {WinRate:P2}\n" +
               $"Profit Factor: {ProfitFactor:F2}\n" +
               $"Recovery Factor: {RecoveryFactor:F2}\n" +
               $"Max Drawdown Duration: {MaxDrawdownDuration} days";
    }
}
