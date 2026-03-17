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

namespace Boutquin.Trading.Application.Configuration;

/// <summary>
/// Configuration options for backtest execution.
/// Binds to the "Backtest" section of appsettings.json.
/// </summary>
public sealed class BacktestOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Backtest";

    /// <summary>
    /// The start date of the backtest period (ISO 8601 format: yyyy-MM-dd).
    /// </summary>
    public string StartDate { get; set; } = "2020-01-01";

    /// <summary>
    /// The end date of the backtest period (ISO 8601 format: yyyy-MM-dd).
    /// </summary>
    public string EndDate { get; set; } = "2025-12-31";

    /// <summary>
    /// The base currency for the backtest.
    /// </summary>
    public string BaseCurrency { get; set; } = "USD";

    /// <summary>
    /// The rebalancing frequency.
    /// </summary>
    public string RebalancingFrequency { get; set; } = "Monthly";

    /// <summary>
    /// The portfolio construction model to use.
    /// </summary>
    public string ConstructionModel { get; set; } = "EqualWeight";

    /// <summary>
    /// The annualized risk-free rate as a decimal (e.g., 0.05 for 5%).
    /// Used for Sharpe ratio, Sortino ratio, and CAPM alpha calculations.
    /// Default: 0 (backward-compatible with existing behavior).
    /// </summary>
    public decimal RiskFreeRate { get; set; } = 0m;
}
