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

namespace Boutquin.Trading.Domain.TaxEngine;

/// <summary>
/// Represents a classified dividend for tax purposes.
/// </summary>
/// <param name="ExDate">The ex-dividend date.</param>
/// <param name="PayDate">The dividend payment date.</param>
/// <param name="Symbol">The paying security.</param>
/// <param name="AmountPerShare">
/// The per-share dividend amount, as currency-tagged <see cref="Money"/>. Its
/// <see cref="Money.Currency"/> is the <b>sole</b> currency source for the dividend — there is no
/// separate stored currency field (a single dividend cannot carry two divergent currencies).
/// </param>
/// <param name="DividendType">The jurisdiction-specific dividend classification.</param>
/// <param name="WithholdingTaxRate">
/// The withholding-tax rate as a dimensionless fraction in [0, 1] (a rate, not an amount), or
/// <see langword="null"/> when not applicable. Stays <see cref="decimal"/>.
/// </param>
/// <remarks>
/// <para>
/// Single-currency-source pin: the standalone <c>Currency</c> field that the pre-Money shape
/// carried alongside the bare-<see cref="decimal"/> amount is removed; the currency now lives
/// solely inside <see cref="AmountPerShare"/>. The convenience <see cref="Currency"/> accessor is a
/// derived projection of <see cref="AmountPerShare"/>'s currency, never an independently stored
/// value — eliminating the two-field state-space hazard where amount and currency could diverge.
/// </para>
/// <para>
/// Summing <see cref="AmountPerShare"/> across dividends uses <see cref="Money"/> arithmetic, which
/// throws <see cref="Boutquin.Domain.Exceptions.CurrencyMismatchException"/> at the caller when the
/// currencies differ.
/// </para>
/// </remarks>
public sealed record DividendRecord(
    DateOnly ExDate,
    DateOnly PayDate,
    Symbol Symbol,
    Money AmountPerShare,
    DividendType DividendType,
    decimal? WithholdingTaxRate)
{
    /// <summary>
    /// The dividend's currency, derived from <see cref="AmountPerShare"/>. This is a computed
    /// projection of the single currency source, not a separately stored field.
    /// </summary>
    public Currency Currency => AmountPerShare.Currency;
}
