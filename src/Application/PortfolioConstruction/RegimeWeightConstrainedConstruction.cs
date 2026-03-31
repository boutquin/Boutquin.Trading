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

using Boutquin.Trading.Domain.Enums;
using Domain.ValueObjects;

/// <summary>
/// Decorator that applies per-regime, per-asset weight floors and caps to any portfolio construction model.
/// Selects the constraint set matching the current economic regime, then clamps and renormalizes.
/// </summary>
public sealed class RegimeWeightConstrainedConstruction : IPortfolioConstructionModel
{
    private readonly IPortfolioConstructionModel _inner;
    private readonly IReadOnlyDictionary<EconomicRegime, AssetWeightConstraints> _regimeConstraints;
    private readonly EconomicRegime _currentRegime;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegimeWeightConstrainedConstruction"/> class.
    /// </summary>
    /// <param name="inner">The inner construction model whose weights are constrained.</param>
    /// <param name="regimeConstraints">Per-regime constraint sets (floors and caps per asset).</param>
    /// <param name="currentRegime">The current economic regime determining which constraints apply.</param>
    public RegimeWeightConstrainedConstruction(
        IPortfolioConstructionModel inner,
        IReadOnlyDictionary<EconomicRegime, AssetWeightConstraints> regimeConstraints,
        EconomicRegime currentRegime)
    {
        Guard.AgainstNull(() => inner);
        Guard.AgainstNull(() => regimeConstraints);
        Guard.AgainstUndefinedEnumValue(() => currentRegime);

        if (!regimeConstraints.ContainsKey(currentRegime))
        {
            throw new ArgumentException(
                $"Regime constraints dictionary does not contain an entry for the current regime '{currentRegime}'. " +
                "All active regimes must have constraint entries (use empty AssetWeightConstraints for unconstrained regimes).",
                nameof(regimeConstraints));
        }

        // Validate each regime's constraints
        foreach (var (regime, constraints) in regimeConstraints)
        {
            WeightConstrainedConstruction.ValidateConstraints(constraints.Floors, constraints.Caps);
        }

        _inner = inner;
        _regimeConstraints = regimeConstraints;
        _currentRegime = currentRegime;
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
        var constraints = _regimeConstraints[_currentRegime];

        if (constraints.Floors is null or { Count: 0 } && constraints.Caps is null or { Count: 0 })
        {
            return baseWeights;
        }

        var weights = new Dictionary<Asset, decimal>(assets.Count);
        foreach (var asset in assets)
        {
            weights[asset] = baseWeights.GetValueOrDefault(asset, 0m);
        }

        return WeightConstrainedConstruction.ClampAndRenormalize(assets, weights, constraints.Floors, constraints.Caps);
    }
}
