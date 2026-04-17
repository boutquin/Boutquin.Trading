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

using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Results;

namespace Boutquin.Trading.Recipes;

/// <summary>
/// A materialized, read-only dataset containing all data required for a backtest.
/// The backtest engine never fetches data — it receives a fully-resolved dataset.
/// This makes backtests deterministic and testable without any I/O mocking.
/// </summary>
public interface IBacktestDataset
{
    /// <summary>
    /// Daily price bars grouped by date and asset.
    /// </summary>
    SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>> Prices { get; }

    /// <summary>
    /// Daily FX rates grouped by date and currency pair.
    /// </summary>
    SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> FxRates { get; }

    /// <summary>
    /// Dividend payments per asset. Empty until adapters support DividendPayment records.
    /// </summary>
    IReadOnlyDictionary<Symbol, IReadOnlyList<DividendPayment>> Dividends { get; }

    /// <summary>
    /// Corporate actions per asset. Empty until adapters support CorporateAction records.
    /// </summary>
    IReadOnlyDictionary<Symbol, IReadOnlyList<CorporateAction>> CorporateActions { get; }

    /// <summary>
    /// Economic time series keyed by series ID (e.g., FRED series).
    /// </summary>
    IReadOnlyDictionary<string, SortedDictionary<DateOnly, decimal>> EconomicSeries { get; }

    /// <summary>
    /// Factor return series keyed by dataset name (e.g., Fama-French).
    /// </summary>
    IReadOnlyDictionary<string, SortedDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>> FactorSeries { get; }

    /// <summary>
    /// Data provenance records from all pipeline fetches.
    /// </summary>
    IReadOnlyList<DataProvenance> Provenance { get; }

    /// <summary>
    /// Data quality issues from all pipeline fetches.
    /// </summary>
    IReadOnlyList<DataIssue> Issues { get; }
}
