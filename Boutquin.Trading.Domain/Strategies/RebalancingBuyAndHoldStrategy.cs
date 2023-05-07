// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

namespace Boutquin.Trading.Domain.Strategies;

using Boutquin.Domain.Exceptions;
using Boutquin.Domain.Helpers;
using Data;
using Enums;
using Events;
using Interfaces;

/// <summary>
/// A rebalancing buy and hold strategy that periodically adjusts asset allocation to maintain target weights.
/// </summary>
public sealed class RebalancingBuyAndHoldStrategy : IStrategy
{
    public string Name { get; }
    public SortedDictionary<string, int> Positions { get; } = new SortedDictionary<string, int>();
    public SortedDictionary<string, CurrencyCode> Assets { get; }
    public SortedDictionary<CurrencyCode, decimal> TargetCapital { get; set; }
    public SortedDictionary<CurrencyCode, decimal> Cash { get; } = new SortedDictionary<CurrencyCode, decimal>();
    public SortedDictionary<string, SortedDictionary<DateOnly, decimal>> DailyNativeReturns { get; } = new SortedDictionary<string, SortedDictionary<DateOnly, decimal>>();
    public IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; }
    public IPositionSizer PositionSizer { get; }

    private readonly RebalancingFrequency _rebalancingFrequency;
    private readonly decimal _rebalancingThreshold;

    /// <summary>
    /// Initializes a new instance of the RebalancingBuyAndHoldStrategy class.
    /// </summary>
    /// <param name="name">The name of the strategy.</param>
    /// <param name="assets">A list of assets to buy and hold.</param>
    /// <param name="orderPriceCalculationStrategy">The order price calculation strategy to use.</param>
    /// <param name="positionSizer">The position sizer to use for calculating position sizes.</param>
    /// <param name="rebalancingFrequency">The frequency at which the strategy should rebalance its assets.</param>
    /// <param name="rebalancingThreshold">The threshold that triggers a rebalancing event when exceeded. Expressed as a decimal (e.g., 0.05 for 5%).</param>
    /// <exception cref="ArgumentNullException">Thrown when the name, assets, orderPriceCalculationStrategy, or positionSizer is null.</exception>
    /// <exception cref="EmptyOrNullCollectionException">Thrown when the assets collection is empty or null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the rebalancingThreshold is not between 0 and 1.</exception>
    public RebalancingBuyAndHoldStrategy(
        string name,
        SortedDictionary<string, CurrencyCode> assets,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer,
        RebalancingFrequency rebalancingFrequency,
        decimal rebalancingThreshold)
    {
        Guard.AgainstNull(() => name);
        Guard.AgainstEmptyOrNullCollection(() => assets);
        Guard.AgainstNull(() => orderPriceCalculationStrategy);
        Guard.AgainstNull(() => positionSizer);
        Guard.AgainstOutOfRange(() => rebalancingThreshold, 0, 1);

        Name = name;
        Assets = assets;
        OrderPriceCalculationStrategy = orderPriceCalculationStrategy;
        PositionSizer = positionSizer;
        _rebalancingFrequency = rebalancingFrequency;
        _rebalancingThreshold = rebalancingThreshold;
    }

    /// <summary>
    /// Generates signals for the rebalancing buy and hold strategy.
    /// </summary>
    /// <param name="targetCapital">The target capital allocated to the asset.</param>
    /// <param name="historicalMarketData">The historical market data for the assets.</param>
    /// <param name="historicalFxConversionRates">The historical foreign exchange conversion rates.</param>
    /// <returns>An IEnumerable of SignalEvent instances.</returns>
    /// <exception cref="EmptyOrNullDictionaryException">Thrown when targetCapital, historicalMarketData, or historicalFxConversionRates is empty or null.</exception>
    public IEnumerable<SignalEvent> GenerateSignals(
        SortedDictionary<CurrencyCode, decimal> targetCapital,
        SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData,
        SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        // Validate parameters
        Guard.AgainstEmptyOrNullDictionary(() => targetCapital);
        Guard.AgainstEmptyOrNullDictionary(() => historicalMarketData);
        Guard.AgainstEmptyOrNullDictionary(() => historicalFxConversionRates);

        var currentRebalancingDate = historicalMarketData.First().Key;

        while (currentRebalancingDate <= historicalMarketData.Last().Key)
        {
            // Check if it's time to rebalance
            if (IsRebalanceRequired(currentRebalancingDate, historicalMarketData, targetCapital))
            {
                foreach (var asset in Assets.Keys)
                {
                    // Generate rebalance signals
                    yield return new SignalEvent(currentRebalancingDate, Name, asset, SignalType.Long);
                }
            }

            // Move to the next rebalancing date
            currentRebalancingDate = GetNextRebalancingDate(currentRebalancingDate);
        }
    }

    private bool IsRebalanceRequired(
        DateOnly currentDate,
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData,
        IReadOnlyDictionary<CurrencyCode, decimal> targetCapital)
    {
        // Calculate the deviation from the target allocation
        var deviation = CalculateDeviation(currentDate, historicalMarketData, targetCapital);

        // Check if the deviation exceeds the threshold
        return deviation > _rebalancingThreshold;
    }

    private DateOnly GetNextRebalancingDate(DateOnly currentDate) =>
        // Calculate the next rebalancing date based on the rebalancing frequency
        _rebalancingFrequency switch
        {
            RebalancingFrequency.Daily => currentDate.AddDays(1),
            RebalancingFrequency.Weekly => currentDate.AddDays(7),
            RebalancingFrequency.Monthly => currentDate.AddMonths(1),
            RebalancingFrequency.Quarterly => currentDate.AddMonths(3),
            RebalancingFrequency.Annually => currentDate.AddYears(1),
            _ => throw new InvalidOperationException($"Unsupported rebalancing frequency: {_rebalancingFrequency}")
        };

    private decimal CalculateDeviation(
        DateOnly currentDate, 
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData, 
        IReadOnlyDictionary<CurrencyCode, decimal> targetCapital)
    {
        decimal maxDeviation = 0;

        foreach (var asset in Assets.Keys)
        {
            // Get the current market value of the asset
            var currentMarketValue = GetAssetMarketValue(currentDate, asset, historicalMarketData);

            // Get the target allocation of the asset
            var targetAllocation = targetCapital[Assets[asset]];

            // Calculate the percentage difference between the current market value and the target allocation
            var deviation = Math.Abs(currentMarketValue - targetAllocation) / targetAllocation;

            // Update the maximum deviation if needed
            if (deviation > maxDeviation)
            {
                maxDeviation = deviation;
            }
        }

        return maxDeviation;
    }

    private decimal GetAssetMarketValue(
        DateOnly currentDate, 
        string asset, 
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData)
    {
        if (!historicalMarketData.TryGetValue(currentDate, out var assetData) || !assetData.TryGetValue(asset, out var marketData))
        {
            throw new ArgumentException($"No market data available for asset {asset} on {currentDate}");
        }

        // Get the current position size for the asset
        var positionSize = Positions[asset];

        // Calculate the current market value
        var marketValue = positionSize * marketData.Close;

        return marketValue;
    }
}
