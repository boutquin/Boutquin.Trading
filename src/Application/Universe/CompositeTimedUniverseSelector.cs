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
/// Composes multiple universe selectors (timed and plain) with AND logic.
/// For <see cref="ITimedUniverseSelector"/> instances, <see cref="SelectAsOf"/> is used;
/// for plain <see cref="IUniverseSelector"/> instances, <see cref="IUniverseSelector.Select"/> is used (ignoring date).
/// </summary>
public sealed class CompositeTimedUniverseSelector : ITimedUniverseSelector
{
    private readonly IReadOnlyList<IUniverseSelector> _selectors;

    /// <summary>Initializes a new instance with the specified selectors.</summary>
    /// <param name="selectors">The selectors to compose.</param>
    public CompositeTimedUniverseSelector(IReadOnlyList<IUniverseSelector> selectors)
    {
        Guard.AgainstNull(() => selectors);
        _selectors = selectors;
    }

    /// <inheritdoc />
    public IReadOnlyList<Asset> SelectAsOf(IReadOnlyList<Asset> candidates, DateOnly asOfDate)
    {
        Guard.AgainstNull(() => candidates);

        var current = candidates;
        foreach (var selector in _selectors)
        {
            current = selector is ITimedUniverseSelector timed
                ? timed.SelectAsOf(current, asOfDate)
                : selector.Select(current);
        }

        return current;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to <see cref="SelectAsOf"/> with <see cref="DateOnly.MaxValue"/>.
    /// </remarks>
    public IReadOnlyList<Asset> Select(IReadOnlyList<Asset> candidates) =>
        SelectAsOf(candidates, DateOnly.MaxValue);
}
