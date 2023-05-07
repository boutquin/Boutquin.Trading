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
/// A buy and hold strategy implementation that buys the specified assets at the
/// beginning of the investment period and holds them indefinitely.
/// </summary>
public sealed class BuyAndHoldStrategy : IStrategy
{
    public string Name { get; }
    public SortedDictionary<string, int> Positions { get; } = new SortedDictionary<string, int>();
    public SortedDictionary<string, CurrencyCode> Assets { get; }
    public SortedDictionary<CurrencyCode, decimal> TargetCapital { get; set; }
    public SortedDictionary<CurrencyCode, decimal> Cash { get; } = new SortedDictionary<CurrencyCode, decimal>();
    public SortedDictionary<string, SortedDictionary<DateOnly, decimal>> DailyNativeReturns { get; } = new SortedDictionary<string, SortedDictionary<DateOnly, decimal>>();
    public IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; }
    public IPositionSizer PositionSizer { get; }

    /// <summary>
    /// Initializes a new instance of the BuyAndHoldStrategy class.
    /// </summary>
    /// <param name="name">The name of the strategy.</param>
    /// <param name="assets">A list of assets to buy and hold.</param>
    /// <param name="orderPriceCalculationStrategy">The order price calculation strategy to use.</param>
    /// <param name="positionSizer">The position sizer to use for calculating position sizes.</param>
    /// <exception cref="ArgumentNullException">Thrown when the name, assets, orderPriceCalculationStrategy, or positionSizer is null.</exception>
    /// <exception cref="EmptyOrNullDictionaryException">Thrown when the assets dictionary is empty or null.</exception>
    public BuyAndHoldStrategy(
        string name,
        SortedDictionary<string, CurrencyCode> assets,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer)
    {
        // Validate parameters
        Guard.AgainstNull(() => name);
        Guard.AgainstEmptyOrNullCollection(() => assets);
        Guard.AgainstNull(() => orderPriceCalculationStrategy);
        Guard.AgainstNull(() => positionSizer);

        Name = name;
        Assets = assets;
        OrderPriceCalculationStrategy = orderPriceCalculationStrategy;
        PositionSizer = positionSizer;
    }

    /// <summary>
    /// Generates signals for the buy and hold strategy.
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

        // Buy and hold strategy only generates buy signals at the beginning of the investment period
        if (Positions.Count != 0)
        {
            yield break;
        }

        var firstDate = historicalMarketData.First().Key;

        foreach (var asset in Assets.Keys)
        {
            yield return new SignalEvent(firstDate, Name, asset, SignalType.Long);
        }
    }
}
