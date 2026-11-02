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

namespace Boutquin.Trading.Domain.Helpers;

/// <summary>
/// The single place the engine converts between its base currency and any other currency,
/// so that every consumer of the FX map reads it the same way round.
/// </summary>
/// <remarks>
/// <para>
/// <b>FX map contract.</b> Every FX map the engine carries — a backtest dataset's <c>FxRates</c>, a
/// <c>MarketEvent</c>'s <c>HistoricalFxConversionRates</c>, a portfolio's <c>HistoricalFxConversionRates</c>,
/// and the <c>historicalFxConversionRates</c> parameter on strategies and position sizers — is keyed by
/// a <em>non-base</em> currency and holds <b>base-currency units per 1 unit of the keyed currency</b>.
/// For a CAD-base portfolio, <c>fx[USD] = 1.30</c> means 1 USD is worth 1.30 CAD. Converting a
/// foreign amount to base therefore <em>multiplies</em> by the rate; converting a base amount to a
/// foreign currency <em>divides</em> by it. The base currency itself is never a key: its rate is 1.
/// </para>
/// <para>
/// In <c>Boutquin.MarketData</c> terms, the value stored under key <c>C</c> is the
/// <c>FxRate.Rate</c> of the pair <c>FxPair(C, base)</c> — "1 unit of the pair's base equals
/// <c>Rate</c> units of its quote".
/// </para>
/// </remarks>
public static class FxConversion
{
    /// <summary>
    /// Returns the number of base-currency units one unit of <paramref name="currency"/> is worth
    /// on <paramref name="timestamp"/>.
    /// </summary>
    /// <param name="historicalFxConversionRates">The engine's FX map (see the class remarks for its contract).</param>
    /// <param name="timestamp">The date whose rate table is read.</param>
    /// <param name="currency">The currency to price in base-currency units.</param>
    /// <param name="baseCurrency">The portfolio's base currency.</param>
    /// <returns>1 when <paramref name="currency"/> is the base currency; otherwise the stored rate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="historicalFxConversionRates"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the map has no rate for <paramref name="currency"/> on <paramref name="timestamp"/>.
    /// </exception>
    public static decimal GetBaseUnitsPerUnit(
        IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates,
        DateOnly timestamp,
        Currency currency,
        Currency baseCurrency)
    {
        ArgumentNullException.ThrowIfNull(historicalFxConversionRates);

        if (currency == baseCurrency)
        {
            return 1m;
        }

        if (historicalFxConversionRates.TryGetValue(timestamp, out var rates) &&
            rates.TryGetValue(currency, out var rate))
        {
            return rate;
        }

        throw new InvalidOperationException(
            $"Conversion rate not found for currency {currency} at timestamp {timestamp}");
    }

    /// <summary>
    /// Converts <paramref name="amount"/> denominated in <paramref name="currency"/> into base-currency units.
    /// </summary>
    /// <param name="amount">The amount to convert, in <paramref name="currency"/>.</param>
    /// <param name="currency">The currency <paramref name="amount"/> is denominated in.</param>
    /// <param name="baseCurrency">The portfolio's base currency.</param>
    /// <param name="historicalFxConversionRates">The engine's FX map (see the class remarks for its contract).</param>
    /// <param name="timestamp">The date whose rate is applied.</param>
    /// <returns><paramref name="amount"/> × (base units per unit of <paramref name="currency"/>).</returns>
    /// <exception cref="InvalidOperationException">Thrown when no rate exists for <paramref name="currency"/> on <paramref name="timestamp"/>.</exception>
    public static decimal ToBase(
        decimal amount,
        Currency currency,
        Currency baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates,
        DateOnly timestamp) =>
        amount * GetBaseUnitsPerUnit(historicalFxConversionRates, timestamp, currency, baseCurrency);

    /// <summary>
    /// Converts <paramref name="baseAmount"/> denominated in the base currency into <paramref name="currency"/> units.
    /// </summary>
    /// <param name="baseAmount">The amount to convert, in the base currency.</param>
    /// <param name="currency">The currency to convert into.</param>
    /// <param name="baseCurrency">The portfolio's base currency.</param>
    /// <param name="historicalFxConversionRates">The engine's FX map (see the class remarks for its contract).</param>
    /// <param name="timestamp">The date whose rate is applied.</param>
    /// <returns><paramref name="baseAmount"/> ÷ (base units per unit of <paramref name="currency"/>).</returns>
    /// <exception cref="InvalidOperationException">Thrown when no rate exists for <paramref name="currency"/> on <paramref name="timestamp"/>.</exception>
    public static decimal FromBase(
        decimal baseAmount,
        Currency currency,
        Currency baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates,
        DateOnly timestamp) =>
        baseAmount / GetBaseUnitsPerUnit(historicalFxConversionRates, timestamp, currency, baseCurrency);
}
