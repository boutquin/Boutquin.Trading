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

using Boutquin.Domain.Enumerations;
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
    /// Daily FX rates grouped by date, then keyed by <em>non-base</em> currency. Each value is the
    /// number of <b>base-currency units per 1 unit of the keyed currency</b> on that date — for a
    /// CAD-base dataset, <c>FxRates[d][USD] = 1.30</c> means 1 USD was worth 1.30 CAD on <c>d</c>.
    /// </summary>
    /// <remarks>
    /// This is the orientation every engine consumer relies on (see
    /// <see cref="Boutquin.Trading.Domain.Helpers.FxConversion"/>): a foreign amount is multiplied by
    /// the rate to reach base currency. The base currency is never a key. A date with no entry for a
    /// currency the backtest needs is an FX-staleness decision made by <c>BackTest.RunAsync</c>, not by
    /// the dataset.
    /// </remarks>
    SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>> FxRates { get; }

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
