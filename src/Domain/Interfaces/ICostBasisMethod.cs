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

using TaxEngine;

/// <summary>
/// Defines a jurisdiction-specific cost basis tracking method.
/// Implementations: AverageCostBasis (Canada), FifoCostBasis (US default),
/// SpecificIdentificationCostBasis (US optional).
/// </summary>
public interface ICostBasisMethod
{
    /// <summary>
    /// Records a purchase lot.
    /// </summary>
    void RecordPurchase(DateOnly date, decimal quantity, decimal pricePerShare, decimal commission);

    /// <summary>
    /// Computes realized gain/loss for a sale and removes/adjusts the sold lots.
    /// Returns per-lot disposal records for tax reporting.
    /// </summary>
    /// <param name="date">The sale date.</param>
    /// <param name="quantity">The total quantity sold.</param>
    /// <param name="pricePerShare">The sale price per share.</param>
    /// <param name="commission">The commission paid on the sale.</param>
    /// <returns>Per-lot disposal records produced by the method's default relief order.</returns>
    /// <remarks>
    /// This state-changing operation is not idempotent. Calling it twice records two dispositions
    /// and relieves lots twice; callers replaying a trade stream must deduplicate upstream.
    /// </remarks>
    IReadOnlyList<LotDisposal> RecordSale(DateOnly date, decimal quantity, decimal pricePerShare, decimal commission);

    /// <summary>
    /// Computes realized gain or loss for a sale, using an optional per-disposition lot election.
    /// </summary>
    /// <param name="date">The sale date.</param>
    /// <param name="quantity">The total quantity sold.</param>
    /// <param name="pricePerShare">The sale price per share.</param>
    /// <param name="commission">The commission paid on the sale.</param>
    /// <param name="identifiedLots">
    /// Lots identified at or before the sale, or <see langword="null"/> to use the method's default
    /// relief order.
    /// </param>
    /// <returns>Per-lot disposal records for tax reporting.</returns>
    /// <exception cref="ArgumentException">
    /// The quantities in <paramref name="identifiedLots"/> do not sum to <paramref name="quantity"/>.
    /// </exception>
    /// <exception cref="OverflowException">
    /// The sum of the identified quantities exceeds the range of <see cref="decimal"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The implementation does not support lot identification. Pooled methods such as
    /// AverageCostBasis must reject an election rather than silently ignore it.
    /// </exception>
    /// <remarks>
    /// Identification is a per-disposition election, not standing account state. Implementations
    /// that permit specific identification override this overload. This state-changing operation is
    /// not idempotent; callers replaying trade streams must deduplicate upstream.
    /// </remarks>
    IReadOnlyList<LotDisposal> RecordSale(
        DateOnly date,
        decimal quantity,
        decimal pricePerShare,
        decimal commission,
        IReadOnlyList<LotIdentification>? identifiedLots)
    {
        if (identifiedLots is null)
        {
            return RecordSale(date, quantity, pricePerShare, commission);
        }

        var identifiedQuantity = identifiedLots.Sum(lot => lot.Quantity);
        if (identifiedQuantity != quantity)
        {
            throw new ArgumentException(
                $"Identified quantities sum to {identifiedQuantity}, but the sale quantity is {quantity}.",
                nameof(identifiedLots));
        }

        throw new NotSupportedException(
            $"{GetType().Name} does not support per-disposition lot identification.");
    }

    /// <summary>
    /// Adjusts cost basis for return of capital distributions.
    /// If ROC reduces basis below zero, returns the excess as a deemed capital gain.
    /// </summary>
    RocAdjustmentResult AdjustForReturnOfCapital(DateOnly date, decimal rocPerShare);

    /// <summary>
    /// Current total cost basis across all lots.
    /// </summary>
    decimal TotalCostBasis { get; }

    /// <summary>
    /// Current total quantity across all lots.
    /// </summary>
    decimal TotalQuantity { get; }

    /// <summary>
    /// Per-share cost basis (TotalCostBasis / TotalQuantity). For ACB this is the running average.
    /// </summary>
    decimal CostPerShare { get; }
}
