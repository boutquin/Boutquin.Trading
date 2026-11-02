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

namespace Boutquin.Trading.Domain.Events;
/// <summary>
/// The FillEvent record encapsulates the data points for a fill
/// event resulting from the execution of an order for a specific financial asset at a specific point in time,
/// providing information about the fill price, quantity, commission, asset name, timestamp, and strategy name.
/// </summary>
/// <param name="Timestamp">The timestamp of the fill event,
/// represented as a DateOnly object.
/// </param>
/// <param name="Symbol">The name of the financial asset associated
/// with the fill event, represented as a string.
/// </param>
/// <param name="StrategyName">The name of the strategy associated with the fill event,
/// represented as a string.
/// </param>
/// <param name="TradeAction">The trade action (Buy or Sell) for the fill event.</param>
/// <param name="FillPrice">The price at which the financial asset was filled, represented as a decimal value.
/// </param>
/// <param name="Quantity">The quantity of the financial asset filled, represented as an integer.
/// </param>
/// <param name="Commission">The commission associated with the fill event, represented as a decimal value.
/// </param>
// A2 fix: Added TradeAction parameter to distinguish buy vs sell fills
public sealed record FillEvent(
    DateOnly Timestamp,
    Symbol Symbol,
    string StrategyName,
    TradeAction TradeAction,
    decimal FillPrice,
    int Quantity,
    decimal Commission) : IFinancialEvent
{
    /// <summary>
    /// Projects <see cref="FillPrice"/> as a currency-tagged <see cref="Money"/> for reporting.
    /// </summary>
    /// <param name="currency">
    /// The currency in which the fill executed (the paying asset's native currency). The caller
    /// supplies it because <see cref="FillEvent"/> stays <see cref="decimal"/>-backed — it is
    /// produced inside the fill pipeline adjacent to the per-bar hot loop, which remains
    /// <see cref="decimal"/>. Only this reporting projection is <see cref="Money"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Money"/> whose <see cref="Money.Amount"/> equals <see cref="FillPrice"/> and
    /// whose <see cref="Money.Currency"/> is <paramref name="currency"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a pure boundary projection: it allocates a value-type <see cref="Money"/> and never
    /// mutates the <see cref="FillEvent"/>. It exists so that fill prices can flow into
    /// currency-safe <see cref="Money"/> aggregation (reports, ledgers) without the underlying
    /// event ever carrying <see cref="Money"/> through the hot path.
    /// </para>
    /// </remarks>
    public Money FillPriceAsMoney(Currency currency) => new(FillPrice, currency);

    /// <summary>
    /// Projects <see cref="Commission"/> as a currency-tagged <see cref="Money"/> for reporting.
    /// </summary>
    /// <param name="currency">
    /// The currency in which the commission was charged. Supplied by the caller for the same
    /// reason as <see cref="FillPriceAsMoney"/>: the field stays <see cref="decimal"/>-backed and
    /// only the reporting projection is <see cref="Money"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Money"/> whose <see cref="Money.Amount"/> equals <see cref="Commission"/> and
    /// whose <see cref="Money.Currency"/> is <paramref name="currency"/>.
    /// </returns>
    public Money CommissionAsMoney(Currency currency) => new(Commission, currency);
}
