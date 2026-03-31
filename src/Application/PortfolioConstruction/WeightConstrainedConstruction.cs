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

namespace Boutquin.Trading.Application.PortfolioConstruction;

using Domain.ValueObjects;

/// <summary>
/// Decorator that applies per-asset weight floors and caps to any portfolio construction model.
/// After clamping, weights are iteratively renormalized to sum to 1.0 while respecting bounds.
/// </summary>
public sealed class WeightConstrainedConstruction : IPortfolioConstructionModel
{
    private readonly IPortfolioConstructionModel _inner;
    private readonly IReadOnlyDictionary<Asset, decimal>? _floors;
    private readonly IReadOnlyDictionary<Asset, decimal>? _caps;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeightConstrainedConstruction"/> class.
    /// </summary>
    /// <param name="inner">The inner construction model whose weights are constrained.</param>
    /// <param name="floors">Per-asset minimum weights. Null or empty means no floors.</param>
    /// <param name="caps">Per-asset maximum weights. Null or empty means no caps.</param>
    public WeightConstrainedConstruction(
        IPortfolioConstructionModel inner,
        IReadOnlyDictionary<Asset, decimal>? floors = null,
        IReadOnlyDictionary<Asset, decimal>? caps = null)
    {
        Guard.AgainstNull(() => inner);

        ValidateConstraints(floors, caps);

        _inner = inner;
        _floors = floors;
        _caps = caps;
    }

    /// <summary>
    /// Initializes a new instance using an <see cref="AssetWeightConstraints"/> record.
    /// </summary>
    public WeightConstrainedConstruction(
        IPortfolioConstructionModel inner,
        AssetWeightConstraints constraints)
        : this(inner, constraints?.Floors, constraints?.Caps)
    {
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns)
    {
        if (assets.Count == 0)
        {
            return new Dictionary<Asset, decimal>();
        }

        var baseWeights = _inner.ComputeTargetWeights(assets, returns);

        if (_floors is null or { Count: 0 } && _caps is null or { Count: 0 })
        {
            return baseWeights;
        }

        return ApplyConstraints(assets, baseWeights);
    }

    internal static void ValidateConstraints(
        IReadOnlyDictionary<Asset, decimal>? floors,
        IReadOnlyDictionary<Asset, decimal>? caps)
    {
        if (floors is not null)
        {
            foreach (var (asset, floor) in floors)
            {
                if (floor < 0m || floor > 1m)
                {
                    throw new ArgumentException(
                        $"Floor for '{asset}' must be in [0, 1], got {floor}.",
                        nameof(floors));
                }
            }
        }

        if (caps is not null)
        {
            foreach (var (asset, cap) in caps)
            {
                if (cap < 0m || cap > 1m)
                {
                    throw new ArgumentException(
                        $"Cap for '{asset}' must be in [0, 1], got {cap}.",
                        nameof(caps));
                }
            }
        }

        if (floors is not null)
        {
            var totalFloors = floors.Values.Sum();
            if (totalFloors > 1m + 1e-10m)
            {
                throw new ArgumentException(
                    $"Sum of all floors ({totalFloors}) exceeds 1.0, making constraints infeasible.",
                    nameof(floors));
            }
        }

        if (floors is not null && caps is not null)
        {
            foreach (var (asset, floor) in floors)
            {
                if (caps.TryGetValue(asset, out var cap) && floor > cap)
                {
                    throw new ArgumentException(
                        $"Floor ({floor}) exceeds cap ({cap}) for asset '{asset}'.",
                        nameof(floors));
                }
            }
        }
    }

    internal static IReadOnlyDictionary<Asset, decimal> ClampAndRenormalize(
        IReadOnlyList<Asset> assets,
        Dictionary<Asset, decimal> weights,
        IReadOnlyDictionary<Asset, decimal>? floors,
        IReadOnlyDictionary<Asset, decimal>? caps)
    {
        for (var round = 0; round < 50; round++)
        {
            // Clamp to per-asset bounds
            foreach (var asset in assets)
            {
                var w = weights.GetValueOrDefault(asset, 0m);

                if (floors is not null && floors.TryGetValue(asset, out var floor))
                {
                    w = Math.Max(w, floor);
                }

                if (caps is not null && caps.TryGetValue(asset, out var cap))
                {
                    w = Math.Min(w, cap);
                }

                w = Math.Max(w, 0m);
                weights[asset] = w;
            }

            // Normalize to sum to 1.0
            var sum = 0m;
            foreach (var asset in assets)
            {
                sum += weights[asset];
            }

            if (sum <= 0m)
            {
                var equalWeight = 1m / assets.Count;
                foreach (var asset in assets)
                {
                    weights[asset] = equalWeight;
                }

                return weights;
            }

            foreach (var asset in assets)
            {
                weights[asset] /= sum;
            }

            // Check feasibility
            var feasible = true;
            foreach (var asset in assets)
            {
                var w = weights[asset];

                if (floors is not null && floors.TryGetValue(asset, out var floor) && w < floor - 1e-14m)
                {
                    feasible = false;
                    break;
                }

                if (caps is not null && caps.TryGetValue(asset, out var cap) && w > cap + 1e-14m)
                {
                    feasible = false;
                    break;
                }
            }

            if (feasible)
            {
                return weights;
            }
        }

        return weights;
    }

    private IReadOnlyDictionary<Asset, decimal> ApplyConstraints(
        IReadOnlyList<Asset> assets,
        IReadOnlyDictionary<Asset, decimal> baseWeights)
    {
        var weights = new Dictionary<Asset, decimal>(assets.Count);
        foreach (var asset in assets)
        {
            weights[asset] = baseWeights.GetValueOrDefault(asset, 0m);
        }

        return ClampAndRenormalize(assets, weights, _floors, _caps);
    }
}
