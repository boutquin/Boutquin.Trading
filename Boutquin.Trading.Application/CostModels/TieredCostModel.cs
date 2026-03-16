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
/// Calculates commission using a tiered rate structure based on trade value.
/// Each tier specifies a maximum trade value threshold and its corresponding commission rate.
/// The applicable tier is the first one whose threshold is greater than or equal to the trade value.
/// </summary>
public sealed class TieredCostModel : ITransactionCostModel
{
    private readonly IReadOnlyList<(decimal MaxTradeValue, decimal Rate)> _tiers;

    /// <summary>
    /// Initializes a new instance of the <see cref="TieredCostModel"/> class.
    /// </summary>
    /// <param name="tiers">
    /// A list of (MaxTradeValue, Rate) tuples sorted by MaxTradeValue ascending.
    /// The last tier's MaxTradeValue should be <see cref="decimal.MaxValue"/> to catch all remaining values.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tiers"/> is empty.</exception>
    public TieredCostModel(IReadOnlyList<(decimal MaxTradeValue, decimal Rate)> tiers)
    {
        Guard.AgainstNull(() => tiers);
        if (tiers.Count == 0)
        {
            throw new ArgumentException("At least one tier must be provided.", nameof(tiers));
        }

        _tiers = tiers;
    }

    /// <inheritdoc />
    public decimal CalculateCommission(decimal fillPrice, int quantity, TradeAction tradeAction)
    {
        var tradeValue = fillPrice * quantity;

        foreach (var (maxTradeValue, rate) in _tiers)
        {
            if (tradeValue <= maxTradeValue)
            {
                return tradeValue * rate;
            }
        }

        // If no tier matched (shouldn't happen if last tier is MaxValue), use the last tier's rate
        return tradeValue * _tiers[^1].Rate;
    }
}
