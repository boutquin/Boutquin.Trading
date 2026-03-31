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
/// Adjusts a base construction model's weights based on economic regime and optional momentum scores.
/// Each regime maps to a set of weight adjustments (tilts) that are applied additively,
/// then the result is re-normalized to sum to 1.0.
/// </summary>
public sealed class TacticalOverlayConstruction : IPortfolioConstructionModel
{
    private readonly IPortfolioConstructionModel _baseModel;
    private readonly IReadOnlyDictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>> _regimeTilts;
    private readonly EconomicRegime _currentRegime;
    private readonly IReadOnlyDictionary<Asset, decimal>? _momentumScores;
    private readonly decimal _momentumStrength;

    /// <summary>
    /// Initializes a new instance of the <see cref="TacticalOverlayConstruction"/> class.
    /// </summary>
    /// <param name="baseModel">The base portfolio construction model whose weights are adjusted.</param>
    /// <param name="regimeTilts">Per-regime additive weight adjustments for each asset.</param>
    /// <param name="currentRegime">The current economic regime.</param>
    /// <param name="momentumScores">Optional momentum scores per asset. Positive = overweight, negative = underweight.</param>
    /// <param name="momentumStrength">Scaling factor for momentum-based tilts (0 = no momentum effect). Default 0.1.</param>
    public TacticalOverlayConstruction(
        IPortfolioConstructionModel baseModel,
        IReadOnlyDictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>> regimeTilts,
        EconomicRegime currentRegime,
        IReadOnlyDictionary<Asset, decimal>? momentumScores = null,
        decimal momentumStrength = 0.1m)
    {
        Guard.AgainstNull(() => baseModel);
        Guard.AgainstNull(() => regimeTilts);
        Guard.AgainstUndefinedEnumValue(() => currentRegime);

        if (momentumStrength < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(momentumStrength), "Momentum strength must be non-negative.");
        }

        if (!regimeTilts.ContainsKey(currentRegime))
        {
            throw new ArgumentException(
                $"Regime tilts dictionary does not contain an entry for the current regime '{currentRegime}'. " +
                "All possible regimes must have tilt entries (use empty dictionaries for no-tilt regimes).",
                nameof(regimeTilts));
        }

        _baseModel = baseModel;
        _regimeTilts = regimeTilts;
        _currentRegime = currentRegime;
        _momentumScores = momentumScores;
        _momentumStrength = momentumStrength;
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

        // Get base weights
        var baseWeights = _baseModel.ComputeTargetWeights(assets, returns);

        // Apply regime tilts (constructor guarantees _currentRegime exists in _regimeTilts)
        var adjusted = new Dictionary<Asset, decimal>();
        var tilts = _regimeTilts[_currentRegime];

        foreach (var asset in assets)
        {
            var weight = baseWeights.GetValueOrDefault(asset, 0m);

            if (tilts.TryGetValue(asset, out var tilt))
            {
                weight += tilt;
            }

            // Apply momentum overlay
            if (_momentumScores is not null && _momentumScores.TryGetValue(asset, out var momentum))
            {
                weight += momentum * _momentumStrength;
            }

            // Floor at zero (long-only)
            adjusted[asset] = Math.Max(weight, 0m);
        }

        // Re-normalize to sum to 1.0
        var total = adjusted.Values.Sum();
        if (total > 0m)
        {
            foreach (var asset in assets)
            {
                adjusted[asset] /= total;
            }
        }
        else
        {
            // Fallback to equal weight if all adjusted to zero
            var equalWeight = 1m / assets.Count;
            foreach (var asset in assets)
            {
                adjusted[asset] = equalWeight;
            }
        }

        return adjusted;
    }
}
