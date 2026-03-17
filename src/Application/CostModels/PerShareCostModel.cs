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

namespace Boutquin.Trading.Application.CostModels;

/// <summary>
/// Calculates commission as a fixed amount per share traded.
/// </summary>
public sealed class PerShareCostModel : ITransactionCostModel
{
    private readonly decimal _perShareRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerShareCostModel"/> class.
    /// </summary>
    /// <param name="perShareRate">The commission per share (e.g., 0.005 for half a cent per share).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="perShareRate"/> is negative or zero.</exception>
    public PerShareCostModel(decimal perShareRate)
    {
        Guard.AgainstNegativeOrZero(() => perShareRate);
        _perShareRate = perShareRate;
    }

    /// <inheritdoc />
    public decimal CalculateCommission(decimal fillPrice, int quantity, TradeAction tradeAction) =>
        quantity * _perShareRate;
}
