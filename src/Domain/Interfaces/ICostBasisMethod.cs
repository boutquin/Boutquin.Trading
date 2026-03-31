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
    IReadOnlyList<LotDisposal> RecordSale(DateOnly date, decimal quantity, decimal pricePerShare, decimal commission);

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
