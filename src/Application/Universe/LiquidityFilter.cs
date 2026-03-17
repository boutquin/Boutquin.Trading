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
/// Filters assets by minimum average daily trading volume.
/// </summary>
public sealed class LiquidityFilter : IUniverseSelector
{
    private readonly decimal _minAverageDailyVolume;
    private readonly IReadOnlyDictionary<Asset, AssetMetadata> _metadata;

    public LiquidityFilter(decimal minAverageDailyVolume, IReadOnlyDictionary<Asset, AssetMetadata> metadata)
    {
        if (minAverageDailyVolume < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minAverageDailyVolume), "Minimum volume must be non-negative.");
        }

        Guard.AgainstNull(() => metadata);

        _minAverageDailyVolume = minAverageDailyVolume;
        _metadata = metadata;
    }

    public IReadOnlyList<Asset> Select(IReadOnlyList<Asset> candidates)
    {
        Guard.AgainstNull(() => candidates);

        return candidates
            .Where(a => _metadata.TryGetValue(a, out var m) && m.AverageDailyVolume >= _minAverageDailyVolume)
            .ToList();
    }
}
