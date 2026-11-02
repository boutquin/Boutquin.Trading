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

namespace Boutquin.Trading.Domain.Interfaces;
/// <summary>
/// The IStrategy interface defines the structure and behavior of a trading
/// strategy, providing methods for generating trading signals, managing
/// positions, and calculating equity.
/// </summary>
/// <remarks>
/// A custom trading strategy should implement this interface to define
/// its logic and interact with the trading framework.
/// </remarks>
public interface IStrategy
{
    /// <summary>
    /// The name of the strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The current positions of the strategy, represented as a read-only
    /// dictionary where the key is the asset symbol and the value is the
    /// quantity of the asset held.
    /// </summary>
    IReadOnlyDictionary<Symbol, int> Positions { get; }

    /// <summary>
    /// A read-only dictionary of assets and their associated currency codes.
    /// The key is the asset symbol and the value is the asset's currency code.
    /// </summary>
    IReadOnlyDictionary<Symbol, Currency> Assets { get; }

    /// <summary>
    /// The available cash for this strategy as a read-only dictionary, where the key
    /// is the currency code, and the value is the amount of cash available in that currency.
    /// </summary>
    IReadOnlyDictionary<Currency, decimal> Cash { get; }

    /// <summary>
    /// A currency-tagged <see cref="Money"/> projection of <see cref="Cash"/>, where each
    /// value is the bucket amount tagged with that bucket's own currency.
    /// </summary>
    /// <value>
    /// A read-only dictionary keyed by <see cref="Currency"/>; each value is a <see cref="Money"/>
    /// whose <see cref="Money.Amount"/> equals the corresponding <see cref="Cash"/> entry and whose
    /// <see cref="Money.Currency"/> is the same key.
    /// </value>
    /// <remarks>
    /// <para>
    /// This is a boundary projection derived from the backing <see cref="Cash"/> collection — the
    /// stored collection stays <see cref="decimal"/> (hot/internal), and this view is materialized
    /// at the reporting boundary. The default implementation maps each bucket without entering any
    /// per-bar path, so adopting <see cref="Money"/> here cannot perturb backtest throughput.
    /// It never throws: each bucket is tagged with its own currency, so no cross-currency
    /// arithmetic occurs.
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<Currency, Money> CashAsMoney
    {
        get
        {
            var result = new Dictionary<Currency, Money>(Cash.Count);
            foreach (var (currency, amount) in Cash)
            {
                result[currency] = new Money(amount, currency);
            }

            return result;
        }
    }

    /// <summary>
    /// The instance of the IOrderPriceCalculationStrategy associated with the strategy.
    /// </summary>
    IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; }

    /// <summary>
    /// The position sizer associated with the strategy, which is
    /// responsible for determining the size of positions taken in each asset.
    /// </summary>
    IPositionSizer PositionSizer { get; }

    /// <summary>
    /// Generates trading signals for the strategy based on historical market data and foreign exchange conversion rates.
    /// </summary>
    /// <param name="timestamp">The timestamp for the historical data to be used in generating the signals.</param>
    /// <param name="baseCurrency">The base currency used for the calculations and conversions within the strategy.</param>
    /// <param name="historicalMarketData">A dictionary containing historical market data, indexed by date and asset symbols.</param>
    /// <param name="historicalFxConversionRates">FX rate tables by date, each keyed by non-base currency and holding base-currency units per 1 unit of the keyed currency (see <see cref="Helpers.FxConversion"/>).</param>
    /// <returns>A SignalEvent instance containing the generated signals for the strategy.</returns>
    SignalEvent GenerateSignals(
        DateOnly timestamp,
        Currency baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates);

    /// <summary>
    /// Computes the total value of the strategy in the base currency using historical market data and foreign exchange conversion rates.
    /// </summary>
    /// <param name="timestamp">The timestamp for the historical data to be used in calculating the total value.</param>
    /// <param name="baseCurrency">The base currency used for the calculations and conversions within the strategy.</param>
    /// <param name="historicalMarketData">A dictionary containing historical market data, indexed by date and asset symbols.</param>
    /// <param name="historicalFxConversionRates">FX rate tables by date, each keyed by non-base currency and holding base-currency units per 1 unit of the keyed currency (see <see cref="Helpers.FxConversion"/>).</param>
    /// <returns>The total value of the strategy in the base currency.</returns>
    decimal ComputeTotalValue(
        DateOnly timestamp,
        Currency baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates);

    /// <summary>
    /// Computes the total value of the strategy as a currency-tagged <see cref="Money"/> in the
    /// base currency. The additive <see cref="Money"/> companion to <see cref="ComputeTotalValue"/>.
    /// </summary>
    /// <param name="timestamp">The timestamp for the historical data to be used in calculating the total value.</param>
    /// <param name="baseCurrency">The base currency used for the calculations and conversions within the strategy; also the <see cref="Money.Currency"/> of the result.</param>
    /// <param name="historicalMarketData">A dictionary containing historical market data, indexed by date and asset symbols.</param>
    /// <param name="historicalFxConversionRates">FX rate tables by date, each keyed by non-base currency and holding base-currency units per 1 unit of the keyed currency (see <see cref="Helpers.FxConversion"/>).</param>
    /// <returns>
    /// A <see cref="Money"/> whose <see cref="Money.Amount"/> equals
    /// <see cref="ComputeTotalValue"/> for the same arguments and whose <see cref="Money.Currency"/>
    /// is <paramref name="baseCurrency"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a boundary projection: the default implementation tags the existing
    /// <see cref="decimal"/> result with <paramref name="baseCurrency"/>. The per-asset
    /// aggregation and FX conversion inside <see cref="ComputeTotalValue"/> remain
    /// <see cref="decimal"/>; <see cref="Money"/> appears only at the reporting boundary.
    /// </para>
    /// </remarks>
    Money ComputeTotalValueAsMoney(
        DateOnly timestamp,
        Currency baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates)
        => new(
            ComputeTotalValue(timestamp, baseCurrency, historicalMarketData, historicalFxConversionRates),
            baseCurrency);

    /// <summary>
    /// Updates the cash holdings in a particular currency for this strategy.
    /// If the strategy already has a cash balance in the specified currency, it adds the amount to the existing balance.
    /// If the strategy does not have a cash balance in the specified currency, it creates a new balance with the specified amount.
    /// </summary>
    /// <param name="currency">The currency of the cash holdings to be updated.</param>
    /// <param name="amount">The amount by which the cash holdings in the specified currency should be updated.</param>
    void UpdateCash(Currency currency, decimal amount);

    /// <summary>
    /// Updates the cash holdings for this strategy by a currency-tagged <see cref="Money"/> amount,
    /// taking the target currency from <see cref="Money.Currency"/>. The additive <see cref="Money"/>
    /// companion to <see cref="UpdateCash(Currency, decimal)"/>.
    /// </summary>
    /// <param name="amount">
    /// The signed amount to add to the cash bucket named by <see cref="Money.Currency"/>. Its
    /// currency must already be an existing cash bucket of this strategy — crediting a currency the
    /// strategy does not hold is rejected so the wrong bucket is never silently created or credited.
    /// </param>
    /// <exception cref="Boutquin.Domain.Exceptions.CurrencyMismatchException">
    /// Thrown when <paramref name="amount"/>'s currency is not one of this strategy's existing cash
    /// buckets (a wrong-currency credit).
    /// </exception>
    /// <remarks>
    /// <para>
    /// Validates <paramref name="amount"/>'s currency against the existing <see cref="Cash"/>
    /// buckets, then routes to the <see cref="decimal"/> core
    /// <see cref="UpdateCash(Currency, decimal)"/>. This is a construction-boundary guard: it adds
    /// currency safety at the point cash is mutated without changing the <see cref="decimal"/>
    /// storage.
    /// </para>
    /// </remarks>
    void UpdateCash(Money amount)
    {
        if (!Cash.ContainsKey(amount.Currency))
        {
            throw new CurrencyMismatchException(
                $"Cannot credit {amount.Currency} cash: this strategy holds no {amount.Currency} bucket. " +
                $"Existing buckets: {string.Join(", ", Cash.Keys)}.");
        }

        UpdateCash(amount.Currency, amount.Amount);
    }

    /// <summary>
    /// Updates the position quantity for a particular asset in this strategy.
    /// If the strategy already has a position in the specified asset, it adds the quantity to the existing position.
    /// If the strategy does not have a position in the specified asset, it creates a new position with the specified quantity.
    /// </summary>
    /// <param name="asset">The asset whose position quantity needs to be updated.</param>
    /// <param name="quantity">The quantity by which the position in the specified asset should be updated.</param>
    void UpdatePositions(Symbol asset, int quantity);

    /// <summary>
    /// Sets the position quantity for a particular asset to an absolute value.
    /// Used for stock split adjustments where the position must be set rather than incremented.
    /// </summary>
    /// <param name="asset">The asset whose position should be set.</param>
    /// <param name="quantity">The absolute quantity to set.</param>
    void SetPosition(Symbol asset, int quantity);

    /// <summary>
    /// The current position quantity for a specific asset.
    /// Returns zero if no position exists.
    /// </summary>
    /// <param name="asset">The asset to query.</param>
    /// <returns>The current position quantity, or zero if no position exists.</returns>
    int GetPositionQuantity(Symbol asset);
}
