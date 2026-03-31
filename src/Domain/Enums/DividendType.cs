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

namespace Boutquin.Trading.Domain.Enums;

/// <summary>
/// Classifies dividend income by tax treatment.
/// Tax treatment varies dramatically by type and jurisdiction.
/// </summary>
public enum DividendType
{
    /// <summary>Canadian eligible dividend (38% gross-up, enhanced DTC).</summary>
    CanadianEligible,

    /// <summary>Canadian non-eligible dividend (15% gross-up, basic DTC).</summary>
    CanadianNonEligible,

    /// <summary>US qualified dividend (capital gains rate).</summary>
    UsQualified,

    /// <summary>US ordinary dividend (ordinary income rate).</summary>
    UsOrdinary,

    /// <summary>Foreign dividend (ordinary income, may have foreign tax credit).</summary>
    Foreign,

    /// <summary>Return of capital (reduces cost basis, not income).</summary>
    ReturnOfCapital,

    /// <summary>Capital gain distribution (from fund internals).</summary>
    CapitalGainDistribution,

    /// <summary>Unknown or unclassified.</summary>
    Unclassified
}
