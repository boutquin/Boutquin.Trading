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
namespace Boutquin.Trading.Application.PositionSizing;

using Domain.Data;

/// <summary>
/// The FixedWeightPositionSizer class implements the IPositionSizer interface, using fixed asset weights to determine
/// the desired positions. The weights are expressed as a percentage of the total strategy value in the base currency.
/// </summary>
public class FixedWeightPositionSizer : IPositionSizer
{
    private readonly IReadOnlyDictionary<string, decimal> _fixedAssetWeights;
    private readonly CurrencyCode _baseCurrency;

    /// <summary>
    /// Initializes a new instance of the FixedWeightPositionSizer class with the given fixed asset weights and base currency.
    /// </summary>
    /// <param name="fixedAssetWeights">A dictionary containing the fixed asset weights, with asset names as keys and weights as values.</param>
    /// <param name="baseCurrency">The base currency used for calculations and conversions.</param>
    public FixedWeightPositionSizer(IReadOnlyDictionary<string, decimal> fixedAssetWeights, CurrencyCode baseCurrency)
    {
        // Validate parameters
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => fixedAssetWeights); // Throws EmptyOrNullDictionaryException
        Guard.AgainstUndefinedEnumValue(() => baseCurrency); // Throws ArgumentOutOfRangeException

        _fixedAssetWeights = fixedAssetWeights;
        _baseCurrency = baseCurrency;
    }

    /// <summary>
    /// Computes the desired position sizes for all assets in the strategy based on the fixed asset weights and historical market data.
    /// </summary>
    /// <param name="timestamp">The timestamp of the signal event.</param>
    /// <param name="signalType">The type of signal generated by the trading strategy.</param>
    /// <param name="strategy">The strategy instance to use for computing the position sizes.</param>
    /// <param name="historicalMarketData">The historical market data to use for calculations.</param>
    /// <param name="historicalFxConversionRates">The historical foreign exchange conversion rates to use for currency conversions.</param>
    /// <returns>A dictionary containing the desired position sizes for all assets in the strategy.</returns>
    public IReadOnlyDictionary<string, int> ComputePositionSizes(
        DateOnly timestamp,
        IReadOnlyDictionary<string, SignalType> signalType,
        IStrategy strategy,
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>?> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        // Validate parameters
        Guard.AgainstNull(() => signalType);
        Guard.AgainstNull(() => strategy);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalMarketData);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalFxConversionRates);

        var positionSizes = new Dictionary<string, int>();

        // Compute the total value of the strategy in the base currency
        var totalStrategyValue = strategy.ComputeTotalValue(
            timestamp,
            _baseCurrency,
            historicalMarketData, 
            historicalFxConversionRates);

        // Calculate the desired position size for each asset based on the fixed asset weights
        foreach (var asset in strategy.Assets.Keys)
        {
            if (!_fixedAssetWeights.ContainsKey(asset))
            {
                throw new InvalidOperationException($"Fixed asset weight not found for asset '{asset}'.");
            }

            var assetWeight = _fixedAssetWeights[asset];
            var desiredAssetValue = totalStrategyValue * assetWeight;

            if (!historicalMarketData[timestamp].TryGetValue(asset, out var marketData))
            {
                throw new InvalidOperationException($"Market data not found for asset '{asset}' on {timestamp}.");
            }

            var positionSize = (int)(desiredAssetValue / marketData.AdjustedClose);
            positionSizes[asset] = positionSize;
        }

        return positionSizes;
    }
}
