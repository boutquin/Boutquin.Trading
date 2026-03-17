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

namespace Boutquin.Trading.Tests.UnitTests.Helpers;

/// <summary>
/// Represents a test strategy for trading.
/// </summary>
public sealed class TestStrategy : IStrategy
{
    private readonly SortedDictionary<Asset, int> _positions = [];
    private readonly SortedDictionary<CurrencyCode, decimal> _cash = [];

    /// <summary>
    /// Gets or sets the name of the strategy.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, int> Positions
    {
        get => _positions;
        set
        {
            _positions.Clear();
            foreach (var kvp in value)
            {
                _positions[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the assets involved in the strategy.
    /// </summary>
    public IReadOnlyDictionary<Asset, CurrencyCode> Assets { get; set; } = new Dictionary<Asset, CurrencyCode>();

    /// <inheritdoc />
    public IReadOnlyDictionary<CurrencyCode, decimal> Cash
    {
        get => _cash;
        set
        {
            _cash.Clear();
            foreach (var kvp in value)
            {
                _cash[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the strategy for calculating order prices.
    /// </summary>
    public IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; set; } = null!;

    /// <summary>
    /// Gets or sets the position sizer.
    /// </summary>
    public IPositionSizer PositionSizer { get; set; } = null!;

    /// <inheritdoc />
    public SignalEvent GenerateSignals(DateOnly timestamp, CurrencyCode baseCurrency, IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData, IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        return new SignalEvent(timestamp, Name, new Dictionary<Asset, SignalType>());
    }

    /// <inheritdoc />
    public decimal ComputeTotalValue(DateOnly timestamp, CurrencyCode baseCurrency, IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData, IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        Guard.AgainstUndefinedEnumValue(() => baseCurrency);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalMarketData);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalFxConversionRates);

        var totalValue = 0m;

        foreach (var (asset, assetCurrency) in Assets)
        {
            _positions.TryGetValue(asset, out var positionSize);

            if (historicalMarketData.TryGetValue(timestamp, out var assetMarketData) &&
                assetMarketData is not null && assetMarketData.TryGetValue(asset, out var marketData))
            {
                var assetValue = positionSize * marketData.AdjustedClose;

                if (assetCurrency != baseCurrency)
                {
                    if (historicalFxConversionRates.TryGetValue(timestamp, out var fxRates) &&
                        fxRates.TryGetValue(assetCurrency, out var conversionRate))
                    {
                        assetValue *= conversionRate;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Conversion rate not found for currency {assetCurrency} at timestamp {timestamp}");
                    }
                }

                totalValue += assetValue;
            }
            else
            {
                throw new InvalidOperationException($"Market data not found for asset {asset} at timestamp {timestamp}");
            }
        }

        foreach (var cashEntry in _cash)
        {
            var cashCurrency = cashEntry.Key;
            var cashAmount = cashEntry.Value;

            if (cashCurrency != baseCurrency)
            {
                if (historicalFxConversionRates.TryGetValue(timestamp, out var fxRates) &&
                    fxRates.TryGetValue(cashCurrency, out var conversionRate))
                {
                    cashAmount *= conversionRate;
                }
                else
                {
                    throw new InvalidOperationException($"Conversion rate not found for currency {cashCurrency} at timestamp {timestamp}");
                }
            }

            totalValue += cashAmount;
        }

        return totalValue;
    }

    /// <inheritdoc />
    public void UpdateCash(CurrencyCode currency, decimal amount)
    {
        Guard.AgainstUndefinedEnumValue(() => currency);

        if (!_cash.TryAdd(currency, amount))
        {
            _cash[currency] += amount;
        }
    }

    /// <inheritdoc />
    public void UpdatePositions(Asset asset, int quantity)
    {
        if (!_positions.TryAdd(asset, quantity))
        {
            _positions[asset] += quantity;
        }
    }

    /// <inheritdoc />
    public void SetPosition(Asset asset, int quantity)
    {
        _positions[asset] = quantity;
    }

    /// <inheritdoc />
    public int GetPositionQuantity(Asset asset)
    {
        return _positions.GetValueOrDefault(asset, 0);
    }
}
