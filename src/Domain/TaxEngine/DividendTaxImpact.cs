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
/// Computed tax impact of dividend income, including jurisdiction-specific credits.
/// </summary>
/// <param name="GrossIncome">The gross dividend income, as currency-tagged <see cref="Money"/>.</param>
/// <param name="TaxableIncome">The taxable portion of the dividend income, as currency-tagged <see cref="Money"/>.</param>
/// <param name="TaxCredits">The dividend tax credits applied, as currency-tagged <see cref="Money"/>.</param>
/// <param name="EstimatedTax">The estimated tax owed on the dividend income, as currency-tagged <see cref="Money"/>.</param>
/// <remarks>
/// All amount fields are currency-tagged <see cref="Money"/>; combining them with amounts of a
/// different currency uses <see cref="Money"/> arithmetic, which throws
/// <see cref="Boutquin.Domain.Exceptions.CurrencyMismatchException"/> at the caller.
/// </remarks>
public sealed record DividendTaxImpact(
    Money GrossIncome,
    Money TaxableIncome,
    Money TaxCredits,
    Money EstimatedTax);
