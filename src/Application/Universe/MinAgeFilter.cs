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
/// Filters assets by minimum age since inception.
/// </summary>
public sealed class MinAgeFilter : IUniverseSelector
{
    private readonly int _minAgeDays;
    private readonly DateOnly _asOfDate;
    private readonly IReadOnlyDictionary<Asset, AssetMetadata> _metadata;

    /// <summary>Initializes a new instance with the specified minimum age, reference date, and metadata.</summary>
    /// <param name="minAgeDays">The minimum age in days since inception.</param>
    /// <param name="asOfDate">The reference date for age calculation.</param>
    /// <param name="metadata">Asset metadata containing inception dates.</param>
    public MinAgeFilter(int minAgeDays, DateOnly asOfDate, IReadOnlyDictionary<Asset, AssetMetadata> metadata)
    {
        Guard.AgainstNegativeOrZero(() => minAgeDays);
        Guard.AgainstNull(() => metadata);

        _minAgeDays = minAgeDays;
        _asOfDate = asOfDate;
        _metadata = metadata;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Asset> Select(IReadOnlyList<Asset> candidates)
    {
        Guard.AgainstNull(() => candidates);

        return candidates
            .Where(a => _metadata.TryGetValue(a, out var m) &&
                        _asOfDate.DayNumber - m.InceptionDate.DayNumber >= _minAgeDays)
            .ToList();
    }
}
