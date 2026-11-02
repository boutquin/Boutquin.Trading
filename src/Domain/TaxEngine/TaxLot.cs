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

namespace Boutquin.Trading.Domain.TaxEngine;

/// <summary>
/// Represents a single purchase lot for cost basis tracking.
/// Used by FIFO and Specific Identification methods.
/// </summary>
/// <param name="PurchaseDate">The date the lot was acquired.</param>
/// <param name="Quantity">The number of shares in the lot (dimensionless; not a monetary amount).</param>
/// <param name="CostPerShare">The per-share acquisition cost, as currency-tagged <see cref="Money"/>.</param>
/// <param name="Commission">The commission paid to acquire the lot, as currency-tagged <see cref="Money"/>.</param>
/// <remarks>
/// The <see cref="Money"/>-typed amount fields carry their own currency; combining them across
/// lots uses <see cref="Money"/> arithmetic, which throws
/// <see cref="Boutquin.Domain.Exceptions.CurrencyMismatchException"/> at the caller on a currency
/// mismatch. <see cref="Quantity"/> stays a dimensionless <see cref="decimal"/>.
/// </remarks>
public sealed record TaxLot(
    DateOnly PurchaseDate,
    decimal Quantity,
    Money CostPerShare,
    Money Commission);
