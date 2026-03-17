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

namespace Boutquin.Trading.Application.PositionSizing;

using Boutquin.Trading.Application.Strategies;
using Domain.ValueObjects;

/// <summary>
/// Position sizer that reads dynamically computed target weights from a
/// <see cref="ConstructionModelStrategy"/> and translates them into position sizes.
/// </summary>
public sealed class DynamicWeightPositionSizer : IPositionSizer
{
    private readonly CurrencyCode _baseCurrency;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicWeightPositionSizer"/> class.
    /// </summary>
    /// <param name="baseCurrency">The base currency used for total value calculations.</param>
    public DynamicWeightPositionSizer(CurrencyCode baseCurrency)
    {
        Guard.AgainstUndefinedEnumValue(() => baseCurrency);

        _baseCurrency = baseCurrency;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, int> ComputePositionSizes(
        DateOnly timestamp,
        IReadOnlyDictionary<Asset, SignalType> signalType,
        IStrategy strategy,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        Guard.AgainstNull(() => signalType);
        Guard.AgainstNull(() => strategy);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalMarketData);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalFxConversionRates);

        // Get target weights from the strategy
        IReadOnlyDictionary<Asset, decimal>? targetWeights = null;
        if (strategy is ConstructionModelStrategy cms)
        {
            targetWeights = cms.LastComputedWeights;
        }

        // Fall back to equal weight if no computed weights available
        targetWeights ??= strategy.Assets.Keys.ToDictionary(
            a => a,
            _ => 1m / strategy.Assets.Count);

        var totalStrategyValue = strategy.ComputeTotalValue(
            timestamp, _baseCurrency, historicalMarketData, historicalFxConversionRates);

        var positionSizes = new Dictionary<Asset, int>();

        foreach (var asset in strategy.Assets.Keys)
        {
            if (!targetWeights.TryGetValue(asset, out var weight))
            {
                weight = 0m;
            }

            var desiredValue = totalStrategyValue * weight;

            if (!historicalMarketData.TryGetValue(timestamp, out var dayData) ||
                dayData is null ||
                !dayData.TryGetValue(asset, out var md))
            {
                throw new InvalidOperationException($"Market data not found for asset '{asset}' on {timestamp}.");
            }

            if (md.AdjustedClose == 0)
            {
                throw new InvalidOperationException($"AdjustedClose is zero for asset '{asset}' on {timestamp}.");
            }

            // M10: Round instead of truncate to avoid systematic downward bias
            positionSizes[asset] = (int)Math.Round(desiredValue / md.AdjustedClose, MidpointRounding.AwayFromZero);
        }

        return positionSizes;
    }
}
