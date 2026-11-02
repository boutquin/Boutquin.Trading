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
/// Prices the cost of a spot FX conversion, the FX counterpart of <see cref="ITransactionCostModel"/>.
/// </summary>
public interface IFxCostModel
{
    /// <summary>
    /// Computes the commission a brokerage charges for exchanging <paramref name="sellAmount"/> of
    /// <paramref name="sellCurrency"/> for <paramref name="buyAmount"/> of <paramref name="buyCurrency"/>.
    /// </summary>
    /// <param name="sellCurrency">The currency paid away.</param>
    /// <param name="sellAmount">Units of <paramref name="sellCurrency"/> exchanged, before commission.</param>
    /// <param name="buyCurrency">The currency received.</param>
    /// <param name="buyAmount">Units of <paramref name="buyCurrency"/> received.</param>
    /// <returns>The commission, denominated in <paramref name="sellCurrency"/>; never negative.</returns>
    decimal CalculateCommission(Currency sellCurrency, decimal sellAmount, Currency buyCurrency, decimal buyAmount);
}
