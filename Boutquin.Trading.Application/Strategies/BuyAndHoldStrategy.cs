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
/// Represents a simple buy and hold strategy that generates buy signals on the initial timestamp and holds the positions throughout.
/// </summary>
public sealed class BuyAndHoldStrategy : StrategyBase
{
    private readonly DateOnly _initialTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyAndHoldStrategy"/> class with the provided parameters.
    /// </summary>
    /// <param name="name">The name of the strategy.</param>
    /// <param name="assets">A dictionary of assets and their corresponding currency codes.</param>
    /// <param name="cash">A sorted dictionary of cash amounts per currency code.</param>
    /// <param name="initialTimestamp">The initial timestamp when the strategy starts.</param>
    /// <param name="orderPriceCalculationStrategy">An instance of IOrderPriceCalculationStrategy to calculate order prices.</param>
    /// <param name="positionSizer">An instance of IPositionSizer to compute position sizes.</param>
    public BuyAndHoldStrategy(
        string name,
        IReadOnlyDictionary<Asset, CurrencyCode> assets,
        SortedDictionary<CurrencyCode, decimal> cash,
        DateOnly initialTimestamp,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer)
        : base(name, assets, cash, orderPriceCalculationStrategy, positionSizer)
    {
        _initialTimestamp = initialTimestamp;
    }

    /// <summary>
    /// Generates buy signals for all assets on the initial timestamp, and no-op signals afterwards.
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

        if (timestamp != _initialTimestamp)
        {
            return new SignalEvent(timestamp, Name, signalEvents);
        }

        foreach (var asset in Assets.Keys)
        {
            signalEvents.Add(asset, SignalType.Underweight);
        }

        return new SignalEvent(timestamp, Name, signalEvents);
    }
}
