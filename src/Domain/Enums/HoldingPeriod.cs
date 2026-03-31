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
/// Classifies the holding period of a disposed asset for tax purposes.
/// US tax law distinguishes short-term (≤1 year) from long-term (&gt;1 year).
/// Canadian tax law has no holding period distinction.
/// </summary>
public enum HoldingPeriod
{
    /// <summary>Asset held for one year or less (US: taxed at ordinary income rates).</summary>
    ShortTerm,

    /// <summary>Asset held for more than one year (US: taxed at preferential capital gains rates).</summary>
    LongTerm,

    /// <summary>Holding period not applicable (e.g., Canadian disposals where no distinction is made).</summary>
    NotApplicable
}
