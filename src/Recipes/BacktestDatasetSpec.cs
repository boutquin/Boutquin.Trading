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
    public Currency BaseCurrency { get; init; } = Currency.USD;

    /// <summary>
    /// Currency codes for non-base assets. For each entry <c>C</c> the builder requests the pair
    /// <c>FxPair(C, BaseCurrency)</c> and stores its rate under key <c>C</c> in
    /// <see cref="IBacktestDataset.FxRates"/> — base-currency units per 1 unit of <c>C</c>.
    /// </summary>
    public IReadOnlyList<Currency> ForeignCurrencies { get; init; } = [];

    /// <summary>
    /// Extra calendar days of FX history requested <em>before</em> <see cref="StartDate"/>, for the
    /// <see cref="ForeignCurrencies"/> pairs only. Default <c>0</c>: the FX request covers exactly the
    /// price window.
    /// </summary>
    /// <remarks>
    /// <c>BackTest.RunAsync</c> never back-fills an FX series, and when it is allowed to carry a print
    /// forward (<c>maxFxRateStalenessDays &gt; 0</c>) it can only carry one it has. A first bar on which
    /// the FX source published nothing — a Canadian bank holiday for Bank of Canada USD/CAD — therefore
    /// aborts a run whose FX request starts on the same day, however generous the staleness bound. A
    /// lead of a week or so gives the resolver a print to carry. Ignored when
    /// <see cref="ForeignCurrencies"/> is empty.
    /// </remarks>
    public int FxLeadDays { get; init; }

    /// <summary>
    /// FRED series IDs for economic data (e.g., "DGS3MO", "CPIAUCSL").
    /// </summary>
    public IReadOnlyList<string> EconomicSeriesIds { get; init; } = [];

    /// <summary>
    /// Fama-French dataset names for factor data (e.g., "F-F_Research_Data_Factors_daily").
    /// </summary>
    public IReadOnlyList<string> FactorDatasets { get; init; } = [];

    /// <summary>
    /// Optional per-symbol listing-venue hint keyed by ticker (case-insensitive consumer
    /// responsibility). When present and a symbol's ticker is found in the dictionary, the
    /// <see cref="ExchangeCode"/> is propagated onto the outgoing
    /// <see cref="Boutquin.MarketData.Abstractions.Requests.PriceHistoryRequest.Exchange"/>
    /// so adapter-selection orchestration can filter by venue — e.g. route a TSX-listed
    /// ticker (XIC) to TwelveData via <c>mic_code=XTSE</c> instead of Tiingo, which does
    /// not cover Canadian venues. Absent entries or a <see langword="null"/> dictionary
    /// produce venue-neutral requests, matching pre-1.1.0 behaviour.
    /// </summary>
    /// <remarks>
    /// The keying is by ticker string rather than <c>Symbol</c> to avoid coupling the spec
    /// to the consumer's symbol-normalization conventions; the builder does the mapping at
    /// request-construction time. Callers that want all-or-nothing scoping should include
    /// every symbol in the dictionary; callers that want a partial hint (e.g. only CA
    /// tickers) can include only those keys and leave US tickers unhinted.
    /// </remarks>
    public IReadOnlyDictionary<string, ExchangeCode>? SymbolExchanges { get; init; }
}
