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

/// <summary>
/// Filters assets by minimum assets under management (AUM).
/// </summary>
public sealed class MinAumFilter : IUniverseSelector
{
    private readonly decimal _minAumMillions;
    private readonly IReadOnlyDictionary<Symbol, AssetMetadata> _metadata;

    /// <summary>Initializes a new instance with the specified minimum AUM threshold and metadata.</summary>
    /// <param name="minAumMillions">The minimum AUM in millions.</param>
    /// <param name="metadata">Symbol metadata containing AUM data.</param>
    public MinAumFilter(decimal minAumMillions, IReadOnlyDictionary<Symbol, AssetMetadata> metadata)
    {
        if (minAumMillions < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minAumMillions), "Minimum AUM must be non-negative.");
        }

        Guard.AgainstNull(() => metadata);

        _minAumMillions = minAumMillions;
        _metadata = metadata;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Symbol> Select(IReadOnlyList<Symbol> candidates)
    {
        Guard.AgainstNull(() => candidates);

        return candidates
            .Where(a => _metadata.TryGetValue(a, out var m) && m.AumMillions >= _minAumMillions)
            .ToList();
    }
}
