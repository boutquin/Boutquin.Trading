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
/// Defines jurisdiction-specific tax computation rules for capital gains and dividends.
/// Implementations: CanadianTaxJurisdiction, UsTaxJurisdiction.
/// </summary>
public interface ITaxJurisdiction
{
    /// <summary>
    /// Computes tax impact of a list of disposals within a tax year using the jurisdiction's
    /// compatible configured or scalar-rate path.
    /// </summary>
    /// <param name="disposals">The disposals included in the tax calculation.</param>
    /// <param name="marginalTaxRate">The taxpayer's marginal ordinary-income tax rate.</param>
    /// <param name="taxYear">The tax year whose dated rules apply.</param>
    /// <returns>The gross gain or loss, taxable amount, and estimated tax.</returns>
    /// <remarks>
    /// This overload remains suitable for rules without taxable-income bands and for existing
    /// implementations configured with a scalar long-term rate. It does not imply that
    /// <paramref name="marginalTaxRate"/> selects a taxable-income-dependent capital-gains band.
    /// </remarks>
    TaxImpact ComputeTaxImpact(
        IReadOnlyList<LotDisposal> disposals,
        decimal marginalTaxRate,
        int taxYear);

    /// <summary>
    /// Computes tax impact using taxable income and filing status for jurisdictions whose
    /// capital-gains rate depends on income bands.
    /// </summary>
    /// <param name="disposals">The disposals included in the tax calculation.</param>
    /// <param name="marginalTaxRate">The taxpayer's marginal ordinary-income tax rate.</param>
    /// <param name="taxYear">The tax year whose dated rules apply.</param>
    /// <param name="taxableIncome">The taxable income used to select an applicable rate band.</param>
    /// <param name="filingStatus">The filing status used to select threshold values.</param>
    /// <returns>The gross gain or loss, taxable amount, and estimated tax.</returns>
    /// <exception cref="NotSupportedException">
    /// The implementation does not support taxable-income-band selection.
    /// </exception>
    /// <remarks>
    /// Implementations must override this method before claiming taxable-income-band selection.
    /// The default fails closed rather than treating <paramref name="marginalTaxRate"/> as a band
    /// selector. The three-argument overload remains a separate scalar-rate compatibility path.
    /// </remarks>
    TaxImpact ComputeTaxImpact(
        IReadOnlyList<LotDisposal> disposals,
        decimal marginalTaxRate,
        int taxYear,
        decimal taxableIncome,
        FilingStatus filingStatus) =>
        throw new NotSupportedException(
            $"{GetType().Name} does not support taxable-income-band selection.");

    /// <summary>
    /// Computes tax on dividend income by type.
    /// </summary>
    DividendTaxImpact ComputeDividendTax(
        IReadOnlyList<DividendRecord> dividends,
        decimal quantity,
        decimal marginalTaxRate);

    /// <summary>
    /// Returns the appropriate cost basis method for this jurisdiction.
    /// </summary>
    ICostBasisMethod CreateCostBasisMethod();

    /// <summary>
    /// Returns the loss harvesting rule for this jurisdiction.
    /// </summary>
    ILossHarvestingRule CreateLossHarvestingRule();
}
