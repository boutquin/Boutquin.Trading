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

using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Diagnostics;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Abstractions.Results;

using Microsoft.Extensions.Logging;

namespace Boutquin.Trading.Recipes;

/// <summary>
/// Materializes an <see cref="IBacktestDataset"/> by fetching data through
/// the MarketData kernel's <see cref="IDataPipeline"/>.
/// </summary>
public sealed class BacktestDatasetBuilder
{
    private readonly IDataPipeline _pipeline;
    private readonly ILogger<BacktestDatasetBuilder> _logger;

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    /// <param name="pipeline">MarketData kernel pipeline for data retrieval.</param>
    /// <param name="logger">Logger for diagnostic output during dataset construction.</param>
    public BacktestDatasetBuilder(
        IDataPipeline pipeline,
        ILogger<BacktestDatasetBuilder> logger)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Builds a complete backtest dataset from the given specification.
    /// </summary>
    /// <param name="spec">Declarative specification of required data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fully materialized <see cref="IBacktestDataset"/>.</returns>
    public async Task<IBacktestDataset> BuildAsync(
        BacktestDatasetSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var allProvenance = new List<DataProvenance>();
        var allIssues = new List<DataIssue>();
        var range = new DateRange(spec.StartDate, spec.EndDate);

        // 1. Fetch price bars (per-symbol for clean Symbol mapping)
        var prices = new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
        if (spec.Symbols.Count > 0)
        {
            foreach (var asset in spec.Symbols)
            {
                var singleRequest = new PriceHistoryRequest([new Symbol(asset.Ticker)], range);
                DataEnvelope<IReadOnlyList<Bar>> singleEnvelope;
                try
                {
                    singleEnvelope = await _pipeline
                        .ExecuteAsync<PriceHistoryRequest, Bar>(singleRequest, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to fetch price data for {Symbol}", asset.Ticker);
                    allIssues.Add(new DataIssue(new IssueCode("PRICE_FETCH_FAILED"), IssueSeverity.Error,
                        $"No price data for {asset.Ticker}: {ex.Message}"));
                    continue;
                }

                allProvenance.AddRange(singleEnvelope.Provenance);
                allIssues.AddRange(singleEnvelope.Issues);

                foreach (var bar in singleEnvelope.Payload)
                {
                    if (!prices.TryGetValue(bar.Date, out var dayDict))
                    {
                        dayDict = new SortedDictionary<Symbol, Bar>();
                        prices[bar.Date] = dayDict;
                    }

                    dayDict[asset] = bar;
                }
            }
        }

        // 2. Fetch FX rates
        var fxRates = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();
        if (spec.ForeignCurrencies.Count > 0)
        {
            var pairs = spec.ForeignCurrencies
                .Select(fc => new FxPair(spec.BaseCurrency, fc))
                .ToList();

            var fxRequest = new FxHistoryRequest(pairs, range);
            try
            {
                var fxEnvelope = await _pipeline
                    .ExecuteAsync<FxHistoryRequest, FxRate>(fxRequest, cancellationToken)
                    .ConfigureAwait(false);

                allProvenance.AddRange(fxEnvelope.Provenance);
                allIssues.AddRange(fxEnvelope.Issues);

                foreach (var rate in fxEnvelope.Payload)
                {
                    if (!fxRates.TryGetValue(rate.Date, out var dayRates))
                    {
                        dayRates = new SortedDictionary<CurrencyCode, decimal>();
                        fxRates[rate.Date] = dayRates;
                    }

                    dayRates[rate.QuoteCurrency] = rate.Rate;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to fetch FX rates");
                allIssues.Add(new DataIssue(new IssueCode("FX_FETCH_FAILED"), IssueSeverity.Error,
                    $"No FX rate data: {ex.Message}"));
            }
        }

        // 3. Fetch economic series
        //    Scalar series (FRED) don't publish on weekends/holidays. The adapter
        //    correctly skips '.' values but reports them as MISSING_VALUES issues.
        //    These are expected noise, not actionable quality problems — log at Debug
        //    and don't propagate to dataset.Issues.
        var economicSeries = new Dictionary<string, SortedDictionary<DateOnly, decimal>>();
        foreach (var seriesId in spec.EconomicSeriesIds)
        {
            var econRequest = new EconomicSeriesRequest(new EconomicSeriesId(seriesId), range);
            try
            {
                var econEnvelope = await _pipeline
                    .ExecuteAsync<EconomicSeriesRequest, ScalarObservation>(econRequest, cancellationToken)
                    .ConfigureAwait(false);

                allProvenance.AddRange(econEnvelope.Provenance);
                PropagateIssues(econEnvelope.Issues, allIssues, seriesId);

                var series = new SortedDictionary<DateOnly, decimal>();
                foreach (var obs in econEnvelope.Payload)
                {
                    series[obs.Date] = obs.Value;
                }

                economicSeries[seriesId] = series;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to fetch economic series {SeriesId}", seriesId);
                allIssues.Add(new DataIssue(new IssueCode("ECON_FETCH_FAILED"), IssueSeverity.Error,
                    $"No economic data for {seriesId}: {ex.Message}"));
            }
        }

        // 4. Fetch factor series (same missing-value filtering as economic series)
        var factorSeries = new Dictionary<string, SortedDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>>();
        foreach (var datasetName in spec.FactorDatasets)
        {
            var factorRequest = new FactorSeriesRequest(new FactorDatasetId(datasetName), range);
            try
            {
                var factorEnvelope = await _pipeline
                    .ExecuteAsync<FactorSeriesRequest, FactorObservation>(factorRequest, cancellationToken)
                    .ConfigureAwait(false);

                allProvenance.AddRange(factorEnvelope.Provenance);
                PropagateIssues(factorEnvelope.Issues, allIssues, datasetName);

                var series = new SortedDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>();
                foreach (var obs in factorEnvelope.Payload)
                {
                    series[obs.Date] = obs.Factors;
                }

                factorSeries[datasetName] = series;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to fetch factor data for {Dataset}", datasetName);
                allIssues.Add(new DataIssue(new IssueCode("FACTOR_FETCH_FAILED"), IssueSeverity.Error,
                    $"No factor data for {datasetName}: {ex.Message}"));
            }
        }

        // 5. Dividends and corporate actions — empty until adapters support them
        var dividends = new Dictionary<Symbol, IReadOnlyList<DividendPayment>>();
        var corporateActions = new Dictionary<Symbol, IReadOnlyList<CorporateAction>>();

        _logger.LogDebug(
            "Dataset built: {PriceDays} price days, {FxDays} FX days, {EconSeries} econ series, {FactorSeries} factor series, {Issues} issues",
            prices.Count, fxRates.Count, economicSeries.Count, factorSeries.Count, allIssues.Count);

        return new BacktestDataset(
            prices,
            fxRates,
            dividends,
            corporateActions,
            economicSeries,
            factorSeries,
            allProvenance,
            allIssues);
    }

    /// <summary>
    /// Filters expected noise from scalar/factor series issues.
    /// MISSING_VALUES on non-trading days (weekends, holidays) are normal for
    /// economic indicators and factor returns — log at Debug, don't propagate.
    /// </summary>
    private void PropagateIssues(
        IReadOnlyList<DataIssue> sourceIssues,
        List<DataIssue> target,
        string seriesId)
    {
        foreach (var issue in sourceIssues)
        {
            if (issue.Code == new IssueCode("MISSING_VALUES"))
            {
                _logger.LogDebug("Expected missing values for {SeriesId}: {Message}", seriesId, issue.Message);
            }
            else if (issue.Code == IssueCode.UnexpectedGap)
            {
                // Calendar-aware adapters flagged a genuine data gap on a business day.
                // Propagate as Warning so the backtest's data quality gate can surface it.
                _logger.LogWarning("Unexpected data gap for {SeriesId}: {Message}", seriesId, issue.Message);
                target.Add(issue);
            }
            else
            {
                target.Add(issue);
            }
        }
    }
}
