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

using Domain.ValueObjects;

/// <summary>
/// Rebalancing buy and hold strategy that periodically rebalances the portfolio to the initial weights.
/// </summary>
public sealed class RebalancingBuyAndHoldStrategy : StrategyBase
{
    private readonly RebalancingFrequency _rebalancingFrequency;
    private readonly ITradingCalendar? _tradingCalendar;
    private readonly ITimedUniverseSelector? _universeSelector;
    private DateOnly? _lastRebalancingDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="RebalancingBuyAndHoldStrategy"/> class with the provided parameters.
    /// </summary>
    /// <param name="name">The name of the strategy.</param>
    /// <param name="assets">A dictionary of assets and their corresponding currency codes.</param>
    /// <param name="cash">A sorted dictionary of cash amounts per currency code.</param>
    /// <param name="orderPriceCalculationStrategy">An instance of IOrderPriceCalculationStrategy to calculate order prices.</param>
    /// <param name="positionSizer">An instance of IPositionSizer to compute position sizes.</param>
    /// <param name="rebalancingFrequency">The frequency at which the strategy should rebalance its assets.</param>
    /// <param name="tradingCalendar">Optional trading calendar for snapping rebalance dates to trading days.</param>
    /// <param name="universeSelector">Optional universe selector for dynamic universe support (ETFs enter at inception).</param>
    public RebalancingBuyAndHoldStrategy(
        string name,
        IReadOnlyDictionary<Asset, CurrencyCode> assets,
        SortedDictionary<CurrencyCode, decimal> cash,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer,
        RebalancingFrequency rebalancingFrequency,
        ITradingCalendar? tradingCalendar = null,
        ITimedUniverseSelector? universeSelector = null)
        : base(name, assets, cash, orderPriceCalculationStrategy, positionSizer)
    {
        _rebalancingFrequency = rebalancingFrequency;
        _tradingCalendar = tradingCalendar;
        _universeSelector = universeSelector;
    }

    /// <summary>
    /// Generates rebalance signals for all assets on rebalancing dates, and no-op signals otherwise.
    /// </summary>
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

        if (_lastRebalancingDate != null && !IsRebalancingDate(timestamp))
        {
            return new SignalEvent(timestamp, Name, signalEvents);
        }

        // Filter assets by universe selector if configured (dynamic universe support)
        var allAssets = Assets.Keys.ToList();
        var eligibleAssets = _universeSelector is not null
            ? _universeSelector.SelectAsOf(allAssets, timestamp)
            : allAssets;

        foreach (var asset in eligibleAssets)
        {
            signalEvents.Add(asset, SignalType.Rebalance);
        }

        // Assets filtered out by universe selector that have positions → sell signal
        if (_universeSelector is not null)
        {
            foreach (var asset in allAssets.Except(eligibleAssets))
            {
                if (GetPositionQuantity(asset) > 0)
                {
                    signalEvents[asset] = SignalType.Rebalance;
                }
            }
        }

        _lastRebalancingDate = timestamp;

        return new SignalEvent(timestamp, Name, signalEvents);
    }

    private bool IsRebalancingDate(DateOnly timestamp)
    {
        if (_lastRebalancingDate == null)
        {
            return false;
        }

        var nextRebalancingDate = GetNextRebalancingDate(_lastRebalancingDate.Value);

        return timestamp >= nextRebalancingDate;
    }

    private DateOnly GetNextRebalancingDate(DateOnly currentDate)
    {
        var nextDate = _rebalancingFrequency switch
        {
            RebalancingFrequency.Never => DateOnly.MaxValue,
            RebalancingFrequency.Daily => currentDate.AddDays(1),
            RebalancingFrequency.Weekly => currentDate.AddDays(7),
            RebalancingFrequency.Monthly => currentDate.AddMonths(1),
            RebalancingFrequency.Quarterly => currentDate.AddMonths(3),
            RebalancingFrequency.Annually => currentDate.AddYears(1),
            _ => throw new InvalidOperationException($"Unsupported rebalancing frequency: {_rebalancingFrequency}")
        };

        // Snap to next trading day if the computed date falls on a weekend or holiday
        if (_tradingCalendar is not null && nextDate != DateOnly.MaxValue && !_tradingCalendar.IsTradingDay(nextDate))
        {
            nextDate = _tradingCalendar.NextTradingDay(nextDate);
        }

        return nextDate;
    }
}
