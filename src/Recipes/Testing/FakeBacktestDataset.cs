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

namespace Boutquin.Trading.Recipes.Testing;

/// <summary>
/// Deterministic test dataset with pre-built data. No pipeline required.
/// Use for unit tests that need an <see cref="IBacktestDataset"/> without I/O.
/// </summary>
public sealed class FakeBacktestDataset : IBacktestDataset
{
    /// <inheritdoc />
    public SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>> Prices { get; init; } = [];

    /// <inheritdoc />
    public SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> FxRates { get; init; } = [];

    /// <inheritdoc />
    public IReadOnlyDictionary<Symbol, IReadOnlyList<DividendPayment>> Dividends { get; init; } =
        new Dictionary<Symbol, IReadOnlyList<DividendPayment>>();

    /// <inheritdoc />
    public IReadOnlyDictionary<Symbol, IReadOnlyList<CorporateAction>> CorporateActions { get; init; } =
        new Dictionary<Symbol, IReadOnlyList<CorporateAction>>();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SortedDictionary<DateOnly, decimal>> EconomicSeries { get; init; } =
        new Dictionary<string, SortedDictionary<DateOnly, decimal>>();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SortedDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>> FactorSeries { get; init; } =
        new Dictionary<string, SortedDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>>();

    /// <inheritdoc />
    public IReadOnlyList<DataProvenance> Provenance { get; init; } = [];

    /// <inheritdoc />
    public IReadOnlyList<DataIssue> Issues { get; init; } = [];
}
