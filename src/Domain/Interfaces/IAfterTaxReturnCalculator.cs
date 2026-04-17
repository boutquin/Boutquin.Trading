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

using Enums;
using TaxEngine;

/// <summary>
/// Calculates after-tax returns for a position held in a specific account type,
/// accounting for capital gains, dividend taxes, and withholding taxes.
/// </summary>
public interface IAfterTaxReturnCalculator
{
    /// <summary>
    /// Computes the after-tax return given pre-tax performance, disposals, and dividends.
    /// </summary>
    /// <param name="preTaxReturn">The pre-tax return as a decimal (e.g., 0.10 for 10%).</param>
    /// <param name="disposals">Capital gain/loss disposals in the period.</param>
    /// <param name="dividends">Dividend records in the period.</param>
    /// <param name="quantity">Number of shares held.</param>
    /// <param name="accountType">The account type holding the position.</param>
    /// <param name="jurisdiction">The tax jurisdiction for gain/dividend computation.</param>
    /// <param name="withholdingSchedule">The withholding tax schedule for cross-border dividends.</param>
    /// <param name="sourceCountry">Country where the security is domiciled.</param>
    /// <param name="investorCountry">Country where the investor is tax-resident.</param>
    /// <param name="marginalTaxRate">The investor's marginal tax rate.</param>
    /// <param name="taxYear">The tax year for computation.</param>
    /// <returns>An <see cref="AfterTaxResult"/> with pre-tax, after-tax, tax drag, and estimated tax.</returns>
    AfterTaxResult Calculate(
        decimal preTaxReturn,
        IReadOnlyList<LotDisposal> disposals,
        IReadOnlyList<DividendRecord> dividends,
        decimal quantity,
        AccountType accountType,
        ITaxJurisdiction jurisdiction,
        IWithholdingTaxSchedule withholdingSchedule,
        CountryCode sourceCountry,
        CountryCode investorCountry,
        decimal marginalTaxRate,
        int taxYear);
}
