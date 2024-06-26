﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Application.Strategies;

using Domain.ValueObjects;

/// <summary>
/// Rebalancing buy and hold strategy that periodically rebalances the portfolio to the initial weights.
/// </summary>
public sealed class RebalancingBuyAndHoldStrategy : IStrategy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuyAndHoldStrategy"/> class with the provided parameters.
    /// </summary>
    /// <param name="name">The name of the strategy.</param>
    /// <param name="assets">A dictionary of assets and their corresponding currency codes.</param>
    /// <param name="cash">A sorted dictionary of cash amounts per currency code.</param>
    /// <param name="orderPriceCalculationStrategy">An instance of IOrderPriceCalculationStrategy to calculate order prices.</param>
    /// <param name="positionSizer">An instance of IPositionSizer to compute position sizes.</param>
    /// <param name="rebalancingFrequency">The frequency at which the strategy should rebalance its assets.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the baseCurrency is not defined.</exception>
    /// <exception cref="EmptyOrNullDictionaryException">Thrown when assets or cash dictionaries are empty or null.</exception>
    public RebalancingBuyAndHoldStrategy(
        string name,
        IReadOnlyDictionary<Asset, CurrencyCode> assets,
        SortedDictionary<CurrencyCode, decimal> cash,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer,
        RebalancingFrequency rebalancingFrequency)
    {
        // Validate parameters
        Guard.AgainstNullOrWhiteSpace(() => name); // Throws ArgumentNullException
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => assets); // Throws EmptyOrNullDictionaryException
        Guard.AgainstEmptyOrNullDictionary(() => cash); // Throws EmptyOrNullDictionaryException
        Guard.AgainstNull(() => orderPriceCalculationStrategy); // Throws ArgumentNullException
        Guard.AgainstNull(() => positionSizer); // Throws ArgumentNullException

        Name = name;
        Assets = assets;
        Cash = cash;
        OrderPriceCalculationStrategy = orderPriceCalculationStrategy;
        PositionSizer = positionSizer;
        Positions = [];
        _rebalancingFrequency = rebalancingFrequency;
    }

    public string Name { get; }
    public SortedDictionary<Asset, int> Positions { get; }
    public IReadOnlyDictionary<Asset, CurrencyCode> Assets { get; }
    public SortedDictionary<CurrencyCode, decimal> Cash { get; }
    public IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; }
    public IPositionSizer PositionSizer { get; }

    private readonly RebalancingFrequency _rebalancingFrequency;
    private DateOnly? _lastRebalancingDate;

    /// <summary>
    /// Generates buy signals for all assets on the initial timestamp, and no-op signals afterwards.
    /// </summary>
    /// <param name="timestamp">The timestamp for which to generate signals.</param>
    /// <param name="historicalMarketData">The historical market data.</param>
    /// <param name="baseCurrency">The base currency used for converting asset values.</param>
    /// <param name="historicalFxConversionRates">The historical foreign exchange conversion rates.</param>
    /// <returns>A SignalEvent containing the generated signals.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the baseCurrency is not defined.</exception>
    /// <exception cref="EmptyOrNullDictionaryException">Thrown when historicalMarketData or historicalFxConversionRates dictionaries are empty or null.</exception>
    public SignalEvent GenerateSignals(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>?> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => baseCurrency); // Throws ArgumentOutOfRangeException
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalMarketData); // Throws EmptyOrNullDictionaryException
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalFxConversionRates); // Throws EmptyOrNullDictionaryException

        // Create a new SignalEvent instance for the given timestamp
        var signalEvents = new SortedDictionary<Asset, SignalType>();

        // If it's not a rebalancing date, return an empty SignalEvent
        if (_lastRebalancingDate != null && !IsRebalancingDate(timestamp))
        {
            return new SignalEvent(timestamp, Name, signalEvents);
        }

        // If it's a rebalancing date, generate rebalance signals for all assets
        foreach (var asset in Assets.Keys)
        {
            signalEvents.Add(asset, SignalType.Rebalance);
        }

        // Update the last rebalancing date
        _lastRebalancingDate = timestamp;

        return new SignalEvent(timestamp, Name, signalEvents);
    }

    /// <summary>
    /// Determines if the provided timestamp is a rebalancing date based on the last rebalancing date and the rebalancing frequency.
    /// </summary>
    /// <param name="timestamp">The date to check if it's a rebalancing date.</param>
    /// <returns>True if the provided timestamp is a rebalancing date, otherwise false.</returns>
    private bool IsRebalancingDate(DateOnly timestamp)
    {
        if (_lastRebalancingDate == null)
        {
            return false;
        }

        var nextRebalancingDate = GetNextRebalancingDate(_lastRebalancingDate.Value);

        return timestamp >= nextRebalancingDate;
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

    private DateOnly GetPreviousRebalancingDate(DateOnly currentDate) =>
        // Calculate the previous rebalancing date based on the rebalancing frequency
        _rebalancingFrequency switch
        {
            RebalancingFrequency.Daily => currentDate.AddDays(-1),
            RebalancingFrequency.Weekly => currentDate.AddDays(-7),
            RebalancingFrequency.Monthly => currentDate.AddMonths(-1),
            RebalancingFrequency.Quarterly => currentDate.AddMonths(-3),
            RebalancingFrequency.Annually => currentDate.AddYears(-1),
            _ => throw new InvalidOperationException($"Unsupported rebalancing frequency: {_rebalancingFrequency}")
        };
}
