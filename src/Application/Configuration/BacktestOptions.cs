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
/// <remarks>
/// Only properties that are consumed by the DI/runtime pipeline are included here.
/// Backtest parameters like StartDate, EndDate, BaseCurrency, RiskFreeRate, and
/// BurnInEndDate are passed directly to BackTest constructor/RunAsync — they are
/// not part of the DI options pattern because BackTest is not DI-registered.
/// </remarks>
public sealed class BacktestOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Backtest";

    /// <summary>
    /// The portfolio construction model to use.
    /// </summary>
    public string ConstructionModel { get; set; } = "EqualWeight";

    /// <summary>
    /// When true, dividends are automatically reinvested into the paying asset
    /// at the current Close price (whole shares only, fractional cash remains).
    /// Only effective in live mode where raw Close is used for valuation.
    /// In backtest mode, AdjustedClose already embeds dividend returns.
    /// </summary>
    public bool EnableDividendReinvestment { get; set; }

    /// <summary>
    /// Default annual expense ratio in basis points (e.g., 20 = 0.20% per year).
    /// Applied to all assets that do not have a per-asset override in <see cref="AssetExpenseRatiosBps"/>.
    /// Deducted daily (annualRate / 252) from each strategy's cash proportional
    /// to position value, before the equity curve is updated.
    /// </summary>
    public decimal AnnualExpenseRatioBps { get; set; }

    /// <summary>
    /// Per-asset annual expense ratios in basis points, keyed by ticker symbol.
    /// Overrides <see cref="AnnualExpenseRatioBps"/> for the specified assets.
    /// Example: <c>{ "VTI": 3, "VXUS": 7, "BND": 3 }</c> for Vanguard ETFs.
    /// </summary>
    public Dictionary<string, decimal> AssetExpenseRatiosBps { get; set; } = [];
}
