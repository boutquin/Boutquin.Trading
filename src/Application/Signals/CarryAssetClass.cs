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

namespace Boutquin.Trading.Application.Signals;

/// <summary>
/// The carry asset class that selects which long-only ETF carry model
/// <see cref="CarrySignal"/> applies to a given asset (Koijen, Moskowitz, Pedersen &amp; Vrugt, 2018).
/// </summary>
/// <remarks>
/// <para>Carry is asset-class specific — the return an asset earns if prices and curves stay unchanged
/// takes a different observable form per class (Koijen et al. 2018, §I). <see cref="CarrySignal"/> is
/// constructed with a per-symbol map from <see cref="Boutquin.MarketData.Abstractions.ReferenceData.Symbol"/>
/// to this enum, so each asset is scored by the carry model appropriate to its class. An asset absent
/// from the map has no determinable carry model and is omitted from the scored cross-section (the
/// absent-key rule of <see cref="ICrossSectionalSignal"/>).</para>
/// <list type="bullet">
///   <item><description><see cref="Bond"/> — carry = yield + roll-down, approximated from two observable
///   term-structure points (e.g. FRED <c>DGS10</c>/<c>DGS2</c>) supplied as market-wide auxiliary series
///   (Koijen et al. eq. 13–15).</description></item>
///   <item><description><see cref="Equity"/> — carry = the asset's trailing income (dividend) yield,
///   supplied per-asset as an auxiliary series keyed by the asset's own symbol (Koijen et al. eq. 8, the
///   dividend-yield component of expected return). REITs use the same model.</description></item>
///   <item><description><see cref="FxHedged"/> — carry = the short-rate differential (foreign minus local),
///   the foreign leg supplied per-asset and the local leg as a market-wide auxiliary series (Koijen et al.
///   eq. 7).</description></item>
/// </list>
/// </remarks>
public enum CarryAssetClass
{
    /// <summary>
    /// A bond (fixed-income) sleeve: carry = yield + roll-down, from two term-structure curve points
    /// supplied as market-wide auxiliary series.
    /// </summary>
    Bond,

    /// <summary>
    /// An equity or REIT sleeve: carry = the asset's trailing income (dividend) yield, supplied per-asset.
    /// </summary>
    Equity,

    /// <summary>
    /// An FX-hedged sleeve: carry = the short-rate differential (foreign short rate minus the local
    /// risk-free rate), the foreign leg supplied per-asset and the local leg market-wide.
    /// </summary>
    FxHedged,
}
