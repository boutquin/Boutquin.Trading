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

/// <summary>
/// Returns the withholding tax rate for cross-border dividends
/// based on source country, investor country, and account type.
/// Implementation: CanadaUsWithholdingSchedule (Canada-US tax treaty rates).
/// </summary>
public interface IWithholdingTaxSchedule
{
    /// <summary>
    /// Returns the withholding tax rate for dividends from a given source country
    /// into a given account type. Returns 0 if no withholding applies.
    /// </summary>
    decimal GetWithholdingRate(
        CountryCode sourceCountry,
        CountryCode investorCountry,
        AccountType accountType);
}
