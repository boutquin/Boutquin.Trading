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
/// A request to convert cash between two of a strategy's currency buckets, emitted by a signal
/// handler when the asset orders of one signal settle in a currency the strategy does not hold
/// enough of. Dispatched to the <c>FxOrderEventHandler</c>, which submits it to the brokerage as an
/// <see cref="FxOrder"/>; the conversion fills on the next bar between that signal's asset sells and
/// asset buys and comes back as an <see cref="FxFillEvent"/>.
/// </summary>
/// <param name="Timestamp">The bar on which the signal was generated.</param>
/// <param name="StrategyName">The strategy whose cash buckets the conversion moves between.</param>
/// <param name="SellCurrency">The currency paid away.</param>
/// <param name="BuyCurrency">The currency received.</param>
/// <param name="BuyAmount">The amount of <see cref="BuyCurrency"/> to receive. Must be positive.</param>
public sealed record FxOrderEvent(
    DateOnly Timestamp,
    string StrategyName,
    Currency SellCurrency,
    Currency BuyCurrency,
    decimal BuyAmount) : IFinancialEvent;
