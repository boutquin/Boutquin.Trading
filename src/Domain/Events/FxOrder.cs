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
/// A spot FX conversion submitted to a brokerage: sell <see cref="SellCurrency"/> to buy
/// <see cref="BuyAmount"/> units of <see cref="BuyCurrency"/>. The brokerage counterpart of
/// <see cref="FxOrderEvent"/>, as <see cref="Order"/> is of <see cref="OrderEvent"/>.
/// </summary>
/// <param name="Timestamp">The bar on which the order was placed. It fills on the next bar.</param>
/// <param name="StrategyName">The strategy whose cash buckets the conversion moves between.</param>
/// <param name="SellCurrency">The currency paid away.</param>
/// <param name="BuyCurrency">The currency received.</param>
/// <param name="BuyAmount">
/// The amount of <see cref="BuyCurrency"/> to receive. The order is denominated on the buy side
/// because the funding need is known when the signal is generated; what it costs in
/// <see cref="SellCurrency"/> depends on the fill-date rate and is the fill's output.
/// </param>
public sealed record FxOrder(
    DateOnly Timestamp,
    string StrategyName,
    Currency SellCurrency,
    Currency BuyCurrency,
    decimal BuyAmount) : IFinancialEvent;
