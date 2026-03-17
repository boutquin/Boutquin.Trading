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
/// Calculates commission as a percentage of the total trade value (fillPrice * quantity).
/// This is the model previously hardcoded in SimulatedBrokerage.
/// </summary>
public sealed class PercentageOfValueCostModel : ITransactionCostModel
{
    private readonly decimal _rate;

    /// <summary>
    /// Initializes a new instance of the <see cref="PercentageOfValueCostModel"/> class.
    /// </summary>
    /// <param name="rate">The commission rate as a decimal (e.g., 0.001 for 0.1%).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rate"/> is negative or zero.</exception>
    public PercentageOfValueCostModel(decimal rate)
    {
        Guard.AgainstNegativeOrZero(() => rate);
        _rate = rate;
    }

    /// <inheritdoc />
    public decimal CalculateCommission(decimal fillPrice, int quantity, TradeAction tradeAction) =>
        fillPrice * quantity * _rate;
}
