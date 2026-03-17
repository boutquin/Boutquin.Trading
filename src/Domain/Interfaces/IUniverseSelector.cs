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
/// Filters a universe of assets based on configurable criteria (AUM, age, liquidity, etc.).
/// </summary>
public interface IUniverseSelector
{
    /// <summary>
    /// Filters the given assets, returning only those that pass the selection criteria.
    /// </summary>
    /// <param name="candidates">The candidate assets to filter.</param>
    /// <returns>The filtered assets that pass all criteria.</returns>
    IReadOnlyList<Asset> Select(IReadOnlyList<Asset> candidates);
}
