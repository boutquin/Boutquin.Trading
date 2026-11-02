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

namespace Boutquin.Trading.Domain.Enums;

/// <summary>
/// How a signal handler funds the asset orders of one signal when they settle in currencies the
/// strategy does not hold enough of. FX orders are explicit, costed trades that fill on the next
/// bar between the asset sells and the asset buys of the same signal.
/// </summary>
/// <remarks>
/// A single-currency portfolio has no non-base currency, so no policy ever emits an FX order for it.
/// The per-currency shortfall a policy acts on is <c>Σ buy cost − Σ expected sell proceeds − cash</c>,
/// priced at the signal bar; the fill-date rate decides what the conversion actually costs, and the
/// FX fill is clipped to the cash the strategy holds in the currency it sells.
/// </remarks>
public enum FxFundingPolicy
{
    /// <summary>
    /// Emit no FX orders. A buy in a currency the strategy holds too little of is quantity-limited
    /// by the fill handler exactly as a cash-short buy in the base currency is.
    /// </summary>
    [Description("No FX orders")]
    None,

    /// <summary>
    /// Fund every shortfall: a non-base shortfall is bought with base currency; a base-currency
    /// shortfall is bought with the non-base surpluses the same signal's sells create. Surplus
    /// non-base cash that funds nothing stays in its own currency.
    /// </summary>
    [Description("Fund shortfalls")]
    FundShortfall,

    /// <summary>
    /// <see cref="FundShortfall"/>, then convert every remaining non-base surplus back to the base
    /// currency, so the strategy never carries idle foreign cash across a rebalance.
    /// </summary>
    [Description("Fund shortfalls and repatriate surpluses")]
    FundShortfallAndRepatriateSurplus,
}
