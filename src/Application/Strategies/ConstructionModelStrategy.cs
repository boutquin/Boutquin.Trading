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

namespace Boutquin.Trading.Application.Strategies;

using Boutquin.Trading.Application.Rebalancing;
using Domain.ValueObjects;

/// <summary>
/// A strategy that uses an <see cref="IPortfolioConstructionModel"/> to compute
/// target weights dynamically at each rebalance point.
/// Rebalancing is triggered by a configurable <see cref="IRebalancingTrigger"/>
/// (calendar-based or threshold-based) combined with a <see cref="RebalancingFrequency"/>.
/// </summary>
public sealed class ConstructionModelStrategy : StrategyBase
{
    private readonly IPortfolioConstructionModel _constructionModel;
    private readonly IRebalancingTrigger _rebalancingTrigger;
    private readonly RebalancingFrequency _rebalancingFrequency;
    private readonly int _lookbackWindow;
    private readonly ILogger<ConstructionModelStrategy> _logger;
    private readonly ITradingCalendar? _tradingCalendar;
    private readonly ITimedUniverseSelector? _universeSelector;
    private readonly SortedDictionary<DateOnly, IReadOnlyDictionary<Asset, decimal>> _targetWeightHistory = [];
    private DateOnly? _lastRebalancingDate;

    /// <summary>
    /// Initializes a new instance (backward-compatible overload).
    /// </summary>
    public ConstructionModelStrategy(
        string name,
        IReadOnlyDictionary<Asset, CurrencyCode> assets,
        SortedDictionary<CurrencyCode, decimal> cash,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer,
        IPortfolioConstructionModel constructionModel,
        RebalancingFrequency rebalancingFrequency,
        IRebalancingTrigger? rebalancingTrigger = null,
        int lookbackWindow = 60,
        ITimedUniverseSelector? universeSelector = null,
        ITradingCalendar? tradingCalendar = null)
        : this(name, assets, cash, orderPriceCalculationStrategy, positionSizer, constructionModel,
               rebalancingFrequency, NullLogger<ConstructionModelStrategy>.Instance, rebalancingTrigger, lookbackWindow, universeSelector, tradingCalendar)
    {
    }

    /// <summary>
    /// Initializes a new instance with structured logging.
    /// </summary>
    public ConstructionModelStrategy(
        string name,
        IReadOnlyDictionary<Asset, CurrencyCode> assets,
        SortedDictionary<CurrencyCode, decimal> cash,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer,
        IPortfolioConstructionModel constructionModel,
        RebalancingFrequency rebalancingFrequency,
        ILogger<ConstructionModelStrategy> logger,
        IRebalancingTrigger? rebalancingTrigger = null,
        int lookbackWindow = 60,
        ITimedUniverseSelector? universeSelector = null,
        ITradingCalendar? tradingCalendar = null)
        : base(name, assets, cash, orderPriceCalculationStrategy, positionSizer)
    {
        Guard.AgainstNull(() => constructionModel);

        _constructionModel = constructionModel;
        _rebalancingTrigger = rebalancingTrigger ?? new CalendarRebalancingTrigger();
        _rebalancingFrequency = rebalancingFrequency;
        _logger = logger ?? NullLogger<ConstructionModelStrategy>.Instance;
        _lookbackWindow = lookbackWindow;
        _universeSelector = universeSelector;
        _tradingCalendar = tradingCalendar;
    }

    /// <summary>
    /// Gets the most recently computed target weights.
    /// </summary>
    public IReadOnlyDictionary<Asset, decimal>? LastComputedWeights { get; private set; }

    /// <summary>
    /// Gets the full history of target allocations keyed by rebalance date.
    /// Each entry is the set of weights that was assigned to <see cref="LastComputedWeights"/> on that date.
    /// </summary>
    public IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<Asset, decimal>> TargetWeightHistory => _targetWeightHistory;

    /// <inheritdoc />
    public override SignalEvent GenerateSignals(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        Guard.AgainstUndefinedEnumValue(() => baseCurrency);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalMarketData);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalFxConversionRates);

        var signalEvents = new SortedDictionary<Asset, SignalType>();

        // Check if it's a rebalancing date
        if (_lastRebalancingDate != null && !IsRebalancingDate(timestamp))
        {
            return new SignalEvent(timestamp, Name, signalEvents);
        }

        // Extract historical returns for each asset, filtered by universe if configured
        var allAssets = Assets.Keys.ToList();
        var assetList = _universeSelector is not null
            ? _universeSelector.SelectAsOf(allAssets, timestamp).ToList()
            : allAssets;
        var returns = assetList.Count > 0
            ? ExtractReturns(assetList, historicalMarketData, timestamp)
            : null;

        // Only proceed if we have enough data and arrays are equal length
        if (returns is not null && returns.All(r => r.Length >= 2) &&
            returns.Select(r => r.Length).Distinct().Count() == 1)
        {
            var targetWeights = _constructionModel.ComputeTargetWeights(assetList, returns);
            LastComputedWeights = targetWeights;
            _logger.LogInformation("Computed target weights on {Date}: {Weights}",
                timestamp, string.Join(", ", targetWeights.Select(kv => $"{kv.Key}={kv.Value:P2}")));

            // M9: Wrap ComputeCurrentWeights in try-catch — failure should fall back, not abort
            Dictionary<Asset, decimal> currentWeights;
            try
            {
                currentWeights = ComputeCurrentWeights(timestamp, baseCurrency, historicalMarketData, historicalFxConversionRates);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "ComputeCurrentWeights failed on {Date}; proceeding with empty weights (will trigger rebalance)", timestamp);
                currentWeights = [];
            }

            // Check if rebalancing trigger fires
            if (_lastRebalancingDate == null || _rebalancingTrigger.ShouldRebalance(currentWeights, targetWeights))
            {
                _logger.LogDebug("Rebalancing triggered on {Date}", timestamp);
                _targetWeightHistory[timestamp] = targetWeights;

                foreach (var asset in assetList)
                {
                    signalEvents.Add(asset, SignalType.Rebalance);
                }

                // Assets filtered out by universe selector that have positions → sell signal
                if (_universeSelector is not null)
                {
                    foreach (var asset in allAssets.Except(assetList))
                    {
                        if (GetPositionQuantity(asset) > 0)
                        {
                            signalEvents[asset] = SignalType.Rebalance;
                            // Set zero weight so position sizer produces a sell
                            targetWeights = new Dictionary<Asset, decimal>(targetWeights) { [asset] = 0m };
                        }
                    }

                    LastComputedWeights = targetWeights;
                }
            }

            // Always advance the rebalancing date when a scheduled date is reached,
            // regardless of whether the trigger fired. Without this, a threshold trigger
            // that doesn't fire leaves _lastRebalancingDate stale, causing the frequency
            // gate (IsRebalancingDate) to pass on every subsequent day.
            _lastRebalancingDate = timestamp;
        }
        else if (_lastRebalancingDate == null)
        {
            // First call — insufficient historical data for the construction model.
            // Initialize with equal weight so the position sizer has weights to work with.
            // The construction model will overwrite these with real weights on the first
            // rebalance date where enough data is available.
            var eligibleAssets = assetList.Count > 0 ? assetList : allAssets;
            var equalWeight = 1m / eligibleAssets.Count;
            LastComputedWeights = eligibleAssets.ToDictionary(a => a, _ => equalWeight);
            _targetWeightHistory[timestamp] = LastComputedWeights;
            _logger.LogDebug(
                "Insufficient data for construction model on {Date} (need {Window} observations). " +
                "Using equal weight for initial allocation. Construction model weights will take " +
                "effect at next rebalance with sufficient data.",
                timestamp, _lookbackWindow);

            foreach (var asset in eligibleAssets)
            {
                signalEvents.Add(asset, SignalType.Underweight);
            }

            _lastRebalancingDate = timestamp;
        }

        return new SignalEvent(timestamp, Name, signalEvents);
    }

    private decimal[][]? ExtractReturns(
        List<Asset> assetList,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData,
        DateOnly asOf)
    {
        // Get sorted dates up to and including asOf
        var dates = historicalMarketData.Keys
            .Where(d => d <= asOf)
            .OrderBy(d => d)
            .ToList();

        if (dates.Count < 2)
        {
            return null;
        }

        // Take at most lookbackWindow+1 dates (to get lookbackWindow returns)
        var windowDates = dates.TakeLast(_lookbackWindow + 1).ToList();

        var returns = new decimal[assetList.Count][];

        for (var i = 0; i < assetList.Count; i++)
        {
            var prices = new List<decimal>();

            foreach (var date in windowDates)
            {
                if (historicalMarketData.TryGetValue(date, out var dayData) &&
                    dayData is not null &&
                    dayData.TryGetValue(assetList[i], out var md))
                {
                    prices.Add(md.AdjustedClose);
                }
            }

            if (prices.Count < 2)
            {
                return null;
            }

            // Compute returns from prices
            var assetReturns = new decimal[prices.Count - 1];
            for (var j = 1; j < prices.Count; j++)
            {
                if (prices[j - 1] == 0m)
                {
                    return null;
                }

                assetReturns[j - 1] = (prices[j] / prices[j - 1]) - 1m;
            }

            returns[i] = assetReturns;
        }

        return returns;
    }

    private Dictionary<Asset, decimal> ComputeCurrentWeights(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        var totalValue = ComputeTotalValue(timestamp, baseCurrency, historicalMarketData, historicalFxConversionRates);
        var weights = new Dictionary<Asset, decimal>();

        if (totalValue <= 0m)
        {
            return weights;
        }

        foreach (var (asset, assetCurrency) in Assets)
        {
            var qty = GetPositionQuantity(asset);
            if (qty == 0 || !historicalMarketData.TryGetValue(timestamp, out var dayData) ||
                dayData is null || !dayData.TryGetValue(asset, out var md))
            {
                weights[asset] = 0m;
                continue;
            }

            var assetValue = qty * md.AdjustedClose;

            // R2C-05 fix: Missing FX rate must throw, not silently use unconverted value
            if (assetCurrency != baseCurrency)
            {
                if (!historicalFxConversionRates.TryGetValue(timestamp, out var fxRates) ||
                    !fxRates.TryGetValue(assetCurrency, out var rate))
                {
                    throw new InvalidOperationException(
                        $"Missing FX rate for {assetCurrency} on {timestamp}. Cannot compute portfolio weights without currency conversion.");
                }

                assetValue *= rate;
            }

            weights[asset] = assetValue / totalValue;
        }

        return weights;
    }

    private bool IsRebalancingDate(DateOnly timestamp)
    {
        if (_lastRebalancingDate == null)
        {
            return false;
        }

        var nextDate = _rebalancingFrequency switch
        {
            RebalancingFrequency.Never => DateOnly.MaxValue,
            RebalancingFrequency.Daily => _lastRebalancingDate.Value.AddDays(1),
            RebalancingFrequency.Weekly => _lastRebalancingDate.Value.AddDays(7),
            RebalancingFrequency.Monthly => _lastRebalancingDate.Value.AddMonths(1),
            RebalancingFrequency.Quarterly => _lastRebalancingDate.Value.AddMonths(3),
            RebalancingFrequency.Annually => _lastRebalancingDate.Value.AddYears(1),
            _ => throw new InvalidOperationException($"Unsupported rebalancing frequency: {_rebalancingFrequency}")
        };

        // Snap to next trading day if the computed date falls on a weekend or holiday
        if (_tradingCalendar is not null && nextDate != DateOnly.MaxValue && !_tradingCalendar.IsTradingDay(nextDate))
        {
            nextDate = _tradingCalendar.NextTradingDay(nextDate);
        }

        return timestamp >= nextDate;
    }
}
