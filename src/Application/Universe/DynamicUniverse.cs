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

using Domain.ValueObjects;

/// <summary>
/// A time-aware universe selector that tracks assets keyed by their eligibility start date.
/// At any given date, only assets whose entry date is on or before the query date are in the universe.
/// </summary>
public sealed class DynamicUniverse : ITimedUniverseSelector
{
    private readonly IReadOnlyDictionary<Asset, DateOnly> _entryDates;

    /// <summary>
    /// Creates a new <see cref="DynamicUniverse"/> with the given entry dates.
    /// </summary>
    /// <param name="entryDates">Map of assets to their eligibility start dates. Must not be null or empty.</param>
    /// <exception cref="EmptyOrNullDictionaryException">Thrown when <paramref name="entryDates"/> is null or empty.</exception>
    public DynamicUniverse(IReadOnlyDictionary<Asset, DateOnly> entryDates)
    {
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => entryDates);
        _entryDates = entryDates;
    }

    /// <inheritdoc />
    public IReadOnlyList<Asset> SelectAsOf(IReadOnlyList<Asset> candidates, DateOnly asOfDate)
    {
        Guard.AgainstNull(() => candidates);

        return candidates
            .Where(a => _entryDates.TryGetValue(a, out var entryDate) && entryDate <= asOfDate)
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to <see cref="SelectAsOf"/> with <see cref="DateOnly.MaxValue"/>,
    /// returning all assets that will ever be eligible.
    /// </remarks>
    public IReadOnlyList<Asset> Select(IReadOnlyList<Asset> candidates) =>
        SelectAsOf(candidates, DateOnly.MaxValue);
}
