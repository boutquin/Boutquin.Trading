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
/// Defines the contract for calculating transaction costs (commissions) on trade executions.
/// </summary>
public interface ITransactionCostModel
{
    /// <summary>
    /// Calculates the commission for a trade execution.
    /// </summary>
    /// <param name="fillPrice">The price at which the order was filled.</param>
    /// <param name="quantity">The number of shares/units traded.</param>
    /// <param name="tradeAction">Whether this is a buy or sell trade.</param>
    /// <returns>The commission amount as a non-negative decimal.</returns>
    decimal CalculateCommission(decimal fillPrice, int quantity, TradeAction tradeAction);
}
