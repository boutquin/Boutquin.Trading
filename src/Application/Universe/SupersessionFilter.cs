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

namespace Boutquin.Trading.Application.Universe;

using Boutquin.Trading.Domain.Analytics;
using Domain.ValueObjects;

/// <summary>
/// Excludes assets whose <see cref="AssetMetadata.SupersededBy"/> replacement
/// is present in the candidate set. When composed with <see cref="DynamicUniverse"/>
/// in a <see cref="CompositeTimedUniverseSelector"/>, this naturally handles
/// time-dependent supersession: the replacement only appears in the candidate set
/// once it becomes eligible by date, at which point this filter removes the predecessor.
/// </summary>
public sealed class SupersessionFilter : IUniverseSelector
{
    private readonly IReadOnlyDictionary<Asset, AssetMetadata> _metadata;

    /// <summary>Initializes a new instance with the specified asset metadata.</summary>
    /// <param name="metadata">Asset metadata containing supersession information.</param>
    public SupersessionFilter(IReadOnlyDictionary<Asset, AssetMetadata> metadata)
    {
        Guard.AgainstNull(() => metadata);
        _metadata = metadata;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Asset> Select(IReadOnlyList<Asset> candidates)
    {
        Guard.AgainstNull(() => candidates);

        var candidateSet = new HashSet<Asset>(candidates);

        return candidates
            .Where(a =>
            {
                if (!_metadata.TryGetValue(a, out var m) || m.SupersededBy is null)
                {
                    return true; // No supersession rule — keep
                }

                // Exclude this asset only if the replacement is in the current candidate set
                return !candidateSet.Contains(m.SupersededBy.Value);
            })
            .ToList();
    }
}
