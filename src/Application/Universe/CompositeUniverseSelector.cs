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
/// Composes multiple universe selectors with AND logic: an asset must pass all selectors to be included.
/// </summary>
public sealed class CompositeUniverseSelector : IUniverseSelector
{
    private readonly IReadOnlyList<IUniverseSelector> _selectors;

    /// <summary>Initializes a new instance with the specified selectors.</summary>
    /// <param name="selectors">The selectors to compose.</param>
    public CompositeUniverseSelector(IReadOnlyList<IUniverseSelector> selectors)
    {
        Guard.AgainstNull(() => selectors);
        _selectors = selectors;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Asset> Select(IReadOnlyList<Asset> candidates)
    {
        Guard.AgainstNull(() => candidates);

        var current = candidates;
        foreach (var selector in _selectors)
        {
            current = selector.Select(current);
        }

        return current;
    }
}
