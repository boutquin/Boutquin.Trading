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
/// Abstract base class for trading strategies that provides common functionality
/// for position management, cash management, and total value computation.
/// </summary>
/// <remarks>
/// Concrete strategies should inherit from this class and implement
/// <see cref="GenerateSignals"/> to define their signal generation logic.
/// The <see cref="Positions"/> and <see cref="Cash"/> properties are exposed
/// as <see cref="IReadOnlyDictionary{TKey,TValue}"/> through the
/// <see cref="IStrategy"/> interface, preventing external mutation while
/// allowing internal state management through dedicated methods.
/// </remarks>
public abstract class StrategyBase : IStrategy
{
    private readonly SortedDictionary<Asset, int> _positions = [];
    private readonly SortedDictionary<CurrencyCode, decimal> _cash;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrategyBase"/> class.
    /// </summary>
    /// <param name="name">The name of the strategy.</param>
    /// <param name="assets">A dictionary of assets and their corresponding currency codes.</param>
    /// <param name="cash">A sorted dictionary of cash amounts per currency code.</param>
    /// <param name="orderPriceCalculationStrategy">An instance of IOrderPriceCalculationStrategy to calculate order prices.</param>
    /// <param name="positionSizer">An instance of IPositionSizer to compute position sizes.</param>
    /// <exception cref="ArgumentException">When <paramref name="name"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="orderPriceCalculationStrategy"/> or <paramref name="positionSizer"/> is null.</exception>
    /// <exception cref="EmptyOrNullDictionaryException">Thrown when assets or cash dictionaries are empty or null.</exception>
    protected StrategyBase(
        string name,
        IReadOnlyDictionary<Asset, CurrencyCode> assets,
        SortedDictionary<CurrencyCode, decimal> cash,
        IOrderPriceCalculationStrategy orderPriceCalculationStrategy,
        IPositionSizer positionSizer)
    {
        Guard.AgainstNullOrWhiteSpace(() => name);
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => assets);
        Guard.AgainstEmptyOrNullDictionary(() => cash);
        Guard.AgainstNull(() => orderPriceCalculationStrategy);
        Guard.AgainstNull(() => positionSizer);

        Name = name;
        Assets = assets;
        // H6: Defensive copy — prevent external mutation via the original reference
        _cash = new SortedDictionary<CurrencyCode, decimal>(cash);
        OrderPriceCalculationStrategy = orderPriceCalculationStrategy;
        PositionSizer = positionSizer;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, int> Positions => _positions;

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, CurrencyCode> Assets { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<CurrencyCode, decimal> Cash => _cash;

    /// <inheritdoc />
    public IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; }

    /// <inheritdoc />
    public IPositionSizer PositionSizer { get; }

    /// <inheritdoc />
    public abstract SignalEvent GenerateSignals(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates);

    /// <inheritdoc />
    public virtual decimal ComputeTotalValue(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
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
        Guard.AgainstNullOrWhiteSpace(() => asset.Ticker);

        if (!_positions.TryAdd(asset, quantity))
        {
            _positions[asset] += quantity;
        }
    }

    /// <inheritdoc />
    public void SetPosition(Asset asset, int quantity)
    {
        Guard.AgainstNullOrWhiteSpace(() => asset.Ticker);

        _positions[asset] = quantity;
    }

    /// <inheritdoc />
    public int GetPositionQuantity(Asset asset)
    {
        Guard.AgainstNullOrWhiteSpace(() => asset.Ticker);

        return _positions.GetValueOrDefault(asset, 0);
    }
}
