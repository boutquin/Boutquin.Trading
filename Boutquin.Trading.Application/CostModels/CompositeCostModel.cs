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
/// Combines multiple cost models by summing their individual commissions.
/// Useful for modeling commission + regulatory fees (e.g., SEC fee + FINRA TAF).
/// </summary>
public sealed class CompositeCostModel : ITransactionCostModel
{
    private readonly IReadOnlyList<ITransactionCostModel> _models;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeCostModel"/> class.
    /// </summary>
    /// <param name="models">The cost models to combine.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="models"/> is empty.</exception>
    public CompositeCostModel(IReadOnlyList<ITransactionCostModel> models)
    {
        Guard.AgainstNull(() => models);
        if (models.Count == 0)
        {
            throw new ArgumentException("At least one cost model must be provided.", nameof(models));
        }

        _models = models;
    }

    /// <inheritdoc />
    public decimal CalculateCommission(decimal fillPrice, int quantity, TradeAction tradeAction) =>
        _models.Sum(m => m.CalculateCommission(fillPrice, quantity, tradeAction));
}
