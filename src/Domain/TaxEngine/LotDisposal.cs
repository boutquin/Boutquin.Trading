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
/// Records the disposal of a tax lot, including realized gain/loss and holding period classification.
/// Produced by <see cref="Interfaces.ICostBasisMethod.RecordSale(DateOnly, decimal, decimal, decimal)"/>
/// or its identification-aware overload.
/// </summary>
/// <param name="PurchaseDate">The date the disposed lot was acquired.</param>
/// <param name="SaleDate">The date the lot was sold.</param>
/// <param name="Quantity">The number of shares disposed (dimensionless; not a monetary amount).</param>
/// <param name="CostPerShare">The per-share acquisition cost, as currency-tagged <see cref="Money"/>.</param>
/// <param name="SalePrice">The per-share sale proceeds, as currency-tagged <see cref="Money"/>.</param>
/// <param name="Commission">The commission charged on the disposal, as currency-tagged <see cref="Money"/>.</param>
/// <param name="RealizedGainLoss">The realized gain (positive) or loss (negative), as currency-tagged <see cref="Money"/>.</param>
/// <param name="HoldingPeriod">The short-/long-term holding-period classification.</param>
/// <remarks>
/// The <see cref="Money"/>-typed amount fields carry their own currency. Combining amounts
/// across <see cref="LotDisposal"/> instances (for example summing
/// <see cref="RealizedGainLoss"/> over a tax year) uses <see cref="Money"/> arithmetic, which
/// throws <see cref="Boutquin.Domain.Exceptions.CurrencyMismatchException"/> at the caller when
/// the operands carry divergent currencies — the silent cross-currency sum the pre-Money
/// <see cref="decimal"/> shape allowed is now a compile- or run-time failure. Rate-like and
/// count-like fields (<see cref="Quantity"/>, dates, <see cref="HoldingPeriod"/>) stay
/// non-monetary.
/// </remarks>
public sealed record LotDisposal(
    DateOnly PurchaseDate,
    DateOnly SaleDate,
    decimal Quantity,
    Money CostPerShare,
    Money SalePrice,
    Money Commission,
    Money RealizedGainLoss,
    Enums.HoldingPeriod HoldingPeriod);
