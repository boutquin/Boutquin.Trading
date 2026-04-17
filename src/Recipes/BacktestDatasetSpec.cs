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

using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Trading.Recipes;

/// <summary>
/// Declarative specification of what data a backtest requires.
/// Passed to <see cref="BacktestDatasetBuilder"/> to materialize an <see cref="IBacktestDataset"/>.
/// </summary>
public sealed record BacktestDatasetSpec
{
    /// <summary>
    /// Ticker symbols for equity/ETF price history.
    /// </summary>
    public required IReadOnlyList<Symbol> Symbols { get; init; }

    /// <summary>
    /// Start date for all data series (inclusive).
    /// </summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>
    /// End date for all data series (inclusive).
    /// </summary>
    public required DateOnly EndDate { get; init; }

    /// <summary>
    /// Base currency for FX rate resolution. FX pairs are derived from
    /// asset currencies that differ from this base.
    /// </summary>
    public CurrencyCode BaseCurrency { get; init; } = CurrencyCode.USD;

    /// <summary>
    /// Currency codes for non-base assets. Used to derive FX pairs.
    /// </summary>
    public IReadOnlyList<CurrencyCode> ForeignCurrencies { get; init; } = [];

    /// <summary>
    /// FRED series IDs for economic data (e.g., "DGS3MO", "CPIAUCSL").
    /// </summary>
    public IReadOnlyList<string> EconomicSeriesIds { get; init; } = [];

    /// <summary>
    /// Fama-French dataset names for factor data (e.g., "F-F_Research_Data_Factors_daily").
    /// </summary>
    public IReadOnlyList<string> FactorDatasets { get; init; } = [];
}
