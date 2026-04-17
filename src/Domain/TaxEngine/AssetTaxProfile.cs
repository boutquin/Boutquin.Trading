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
/// Tax-relevant characteristics of an asset used by the asset location optimizer
/// to determine optimal account placement.
/// </summary>
/// <param name="Symbol">The asset ticker.</param>
/// <param name="ExpectedReturn">Expected annual return as a decimal (e.g., 0.08 for 8%).</param>
/// <param name="DividendYield">Annual dividend yield as a decimal.</param>
/// <param name="DividendType">The expected dividend classification for this asset.</param>
/// <param name="TurnoverRate">Annual portfolio turnover rate (affects realized capital gains).</param>
/// <param name="DomicileCountry">Country where the security is domiciled (affects withholding).</param>
public sealed record AssetTaxProfile(
    Symbol Symbol,
    decimal ExpectedReturn,
    decimal DividendYield,
    DividendType DividendType,
    decimal TurnoverRate,
    CountryCode DomicileCountry);
