// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Tests.UnitTests.Helpers;

/// <summary>
/// Represents a test strategy for trading.
/// </summary>
public class TestStrategy : IStrategy
{
    /// <summary>
    /// Gets or sets the name of the strategy.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the positions held in the strategy.
    /// </summary>
    public SortedDictionary<Asset, int> Positions { get; init; } = [];

    /// <summary>
    /// Gets or sets the assets involved in the strategy.
    /// </summary>
    public IReadOnlyDictionary<Asset, CurrencyCode> Assets { get; set; } = new Dictionary<Asset, CurrencyCode>();

    /// <summary>
    /// Gets the cash held in different currencies.
    /// </summary>
    public SortedDictionary<CurrencyCode, decimal> Cash { get; init; } = [];

    /// <summary>
    /// Gets or sets the strategy for calculating order prices.
    /// </summary>
    public IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; set; }

    /// <summary>
    /// Gets or sets the position sizer.
    /// </summary>
    public IPositionSizer PositionSizer { get; set; }

    /// <summary>
    /// Generates signals for trading based on market data and conversion rates.
    /// </summary>
    /// <param name="timestamp">The date of the signal.</param>
    /// <param name="baseCurrency">The base currency for trading.</param>
    /// <param name="historicalMarketData">The historical market data.</param>
    /// <param name="historicalFxConversionRates">The historical foreign exchange conversion rates.</param>
    /// <returns>A signal event.</returns>
    public SignalEvent GenerateSignals(DateOnly timestamp, CurrencyCode baseCurrency, IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>?> historicalMarketData, IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        throw new NotImplementedException();
    }
}
