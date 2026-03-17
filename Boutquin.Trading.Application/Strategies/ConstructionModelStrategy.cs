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
    private DateOnly? _lastRebalancingDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructionModelStrategy"/> class.
    /// </summary>
    /// <param name="name">The name of the strategy.</param>
    /// <param name="assets">A dictionary of assets and their corresponding currency codes.</param>
    /// <param name="cash">A sorted dictionary of cash amounts per currency code.</param>
    /// <param name="orderPriceCalculationStrategy">Order price calculation strategy.</param>
    /// <param name="positionSizer">Position sizer for computing share quantities.</param>
    /// <param name="constructionModel">The portfolio construction model to compute weights.</param>
    /// <param name="rebalancingFrequency">How often to check for rebalancing.</param>
    /// <param name="rebalancingTrigger">The trigger to determine if rebalancing is needed. Defaults to <see cref="CalendarRebalancingTrigger"/>.</param>
    /// <param name="lookbackWindow">Number of historical observations to feed to the construction model. Default 60.</param>
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
        int lookbackWindow = 60)
        : this(name, assets, cash, orderPriceCalculationStrategy, positionSizer, constructionModel,
               rebalancingFrequency, NullLogger<ConstructionModelStrategy>.Instance, rebalancingTrigger, lookbackWindow)
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
        int lookbackWindow = 60)
        : base(name, assets, cash, orderPriceCalculationStrategy, positionSizer)
    {
        Guard.AgainstNull(() => constructionModel);

        _constructionModel = constructionModel;
        _rebalancingTrigger = rebalancingTrigger ?? new CalendarRebalancingTrigger();
        _rebalancingFrequency = rebalancingFrequency;
        _logger = logger ?? NullLogger<ConstructionModelStrategy>.Instance;
        _lookbackWindow = lookbackWindow;
    }

    /// <summary>
    /// Gets the most recently computed target weights.
    /// </summary>
    public IReadOnlyDictionary<Asset, decimal>? LastComputedWeights { get; private set; }

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

        // Extract historical returns for each asset
        var assetList = Assets.Keys.ToList();
        var returns = ExtractReturns(assetList, historicalMarketData, timestamp);

        // Only proceed if we have enough data
        if (returns is not null && returns.All(r => r.Length >= 2))
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

                foreach (var asset in Assets.Keys)
                {
                    signalEvents.Add(asset, SignalType.Rebalance);
                }

                _lastRebalancingDate = timestamp;
            }
        }
        else if (_lastRebalancingDate == null)
        {
            // First call — buy with equal weight as warm-up
            foreach (var asset in Assets.Keys)
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

            if (assetCurrency != baseCurrency &&
                historicalFxConversionRates.TryGetValue(timestamp, out var fxRates) &&
                fxRates.TryGetValue(assetCurrency, out var rate))
            {
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

        return timestamp >= nextDate;
    }
}
