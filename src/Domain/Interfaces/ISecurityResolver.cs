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
/// <summary>
/// Resolves dual-listed securities to their primary listing and
/// determines equivalence across exchanges (e.g., RY on NYSE ↔ RY.TO on TSX).
/// </summary>
public interface ISecurityResolver
{
    /// <summary>
    /// Returns the primary listing for the given asset.
    /// If the asset is not a known secondary listing, returns the asset itself.
    /// </summary>
    /// <param name="asset">The asset to resolve.</param>
    /// <returns>The primary listing asset.</returns>
    Symbol ResolvePrimary(Symbol asset);

    /// <summary>
    /// Determines whether two assets are equivalent (i.e., resolve to the same primary listing).
    /// </summary>
    /// <param name="a">First asset.</param>
    /// <param name="b">Second asset.</param>
    /// <returns><c>true</c> if both assets resolve to the same primary listing.</returns>
    bool AreEquivalent(Symbol a, Symbol b);

    /// <summary>
    /// Returns all known listings for an asset (primary + all secondaries).
    /// </summary>
    /// <param name="asset">Any listing of the security.</param>
    /// <returns>All known listings, including the primary.</returns>
    IReadOnlyList<Symbol> GetAllListings(Symbol asset);
}
