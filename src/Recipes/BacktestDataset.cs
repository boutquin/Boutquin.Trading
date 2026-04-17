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
/// Immutable implementation of <see cref="IBacktestDataset"/>.
/// Constructed by <see cref="BacktestDatasetBuilder"/>.
/// </summary>
public sealed class BacktestDataset : IBacktestDataset
{
    /// <inheritdoc />
    public SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>> Prices { get; }

    /// <inheritdoc />
    public SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> FxRates { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<Symbol, IReadOnlyList<DividendPayment>> Dividends { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<Symbol, IReadOnlyList<CorporateAction>> CorporateActions { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SortedDictionary<DateOnly, decimal>> EconomicSeries { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SortedDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>> FactorSeries { get; }

    /// <inheritdoc />
    public IReadOnlyList<DataProvenance> Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<DataIssue> Issues { get; }

    /// <summary>
    /// Creates a new backtest dataset with the provided data collections.
    /// </summary>
    public BacktestDataset(
        SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>> prices,
        SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> fxRates,
        IReadOnlyDictionary<Symbol, IReadOnlyList<DividendPayment>> dividends,
        IReadOnlyDictionary<Symbol, IReadOnlyList<CorporateAction>> corporateActions,
        IReadOnlyDictionary<string, SortedDictionary<DateOnly, decimal>> economicSeries,
        IReadOnlyDictionary<string, SortedDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>> factorSeries,
        IReadOnlyList<DataProvenance> provenance,
        IReadOnlyList<DataIssue> issues)
    {
        Prices = prices ?? throw new ArgumentNullException(nameof(prices));
        FxRates = fxRates ?? throw new ArgumentNullException(nameof(fxRates));
        Dividends = dividends ?? throw new ArgumentNullException(nameof(dividends));
        CorporateActions = corporateActions ?? throw new ArgumentNullException(nameof(corporateActions));
        EconomicSeries = economicSeries ?? throw new ArgumentNullException(nameof(economicSeries));
        FactorSeries = factorSeries ?? throw new ArgumentNullException(nameof(factorSeries));
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }
}
