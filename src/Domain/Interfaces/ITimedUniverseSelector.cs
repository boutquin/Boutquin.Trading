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

using ValueObjects;

/// <summary>
/// Extends <see cref="IUniverseSelector"/> with time-aware filtering.
/// Returns assets eligible as of a given date.
/// </summary>
public interface ITimedUniverseSelector : IUniverseSelector
{
    /// <summary>
    /// Returns assets eligible as of the given date.
    /// </summary>
    /// <param name="candidates">The candidate assets to filter.</param>
    /// <param name="asOfDate">The date to evaluate eligibility against.</param>
    /// <returns>The filtered assets eligible as of the given date.</returns>
    IReadOnlyList<Asset> SelectAsOf(IReadOnlyList<Asset> candidates, DateOnly asOfDate);
}
