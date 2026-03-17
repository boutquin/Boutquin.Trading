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

using Boutquin.Trading.Domain.Exceptions;
using Domain.ValueObjects;

/// <summary>
/// Weights each asset inversely proportional to its sample volatility: w_i = (1/σ_i) / Σ(1/σ_j).
/// Assets with lower volatility receive higher weights.
/// </summary>
public sealed class InverseVolatilityConstruction : IPortfolioConstructionModel
{
    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns)
    {
        Guard.AgainstNull(() => assets);

        if (assets.Count == 0)
        {
            return new Dictionary<Asset, decimal>();
        }

        if (returns is null || returns.Length != assets.Count)
        {
            throw new ArgumentException("Returns array must have one series per asset.", nameof(returns));
        }

        var inverseVols = new decimal[assets.Count];
        var sumInverseVol = 0m;

        for (var i = 0; i < assets.Count; i++)
        {
            if (returns[i].Length < 2)
            {
                throw new ArgumentException($"Return series for asset {assets[i]} must have at least two observations.", nameof(returns));
            }

            var vol = returns[i].Volatility();

            if (vol == 0m)
            {
                throw new CalculationException($"Volatility is zero for asset {assets[i]}; cannot compute inverse-volatility weight.");
            }

            inverseVols[i] = 1m / vol;
            sumInverseVol += inverseVols[i];
        }

        var weights = new Dictionary<Asset, decimal>(assets.Count);
        for (var i = 0; i < assets.Count; i++)
        {
            weights[assets[i]] = inverseVols[i] / sumInverseVol;
        }

        return weights;
    }
}
