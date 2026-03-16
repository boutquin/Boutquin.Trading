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

namespace Boutquin.Trading.Application.Rebalancing;

using Domain.ValueObjects;

/// <summary>
/// Triggers rebalancing when any asset's weight drifts beyond a configurable band
/// from its target weight.
/// </summary>
public sealed class ThresholdRebalancingTrigger : IRebalancingTrigger
{
    private readonly decimal _threshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdRebalancingTrigger"/> class.
    /// </summary>
    /// <param name="threshold">
    /// The maximum allowed absolute drift from target weight before rebalancing is triggered.
    /// For example, 0.05 means a ±5% band.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="threshold"/> is non-positive.</exception>
    public ThresholdRebalancingTrigger(decimal threshold)
    {
        Guard.AgainstNegativeOrZero(() => threshold);

        _threshold = threshold;
    }

    /// <inheritdoc />
    public bool ShouldRebalance(
        IReadOnlyDictionary<Asset, decimal> currentWeights,
        IReadOnlyDictionary<Asset, decimal> targetWeights)
    {
        Guard.AgainstNull(() => currentWeights);
        Guard.AgainstNull(() => targetWeights);

        // Check all target assets for drift
        foreach (var (asset, targetWeight) in targetWeights)
        {
            var currentWeight = currentWeights.GetValueOrDefault(asset, 0m);
            var drift = Math.Abs(currentWeight - targetWeight);

            if (drift > _threshold)
            {
                return true;
            }
        }

        // Also check current assets not in target (should be zero)
        foreach (var (asset, currentWeight) in currentWeights)
        {
            if (!targetWeights.ContainsKey(asset) && currentWeight > _threshold)
            {
                return true;
            }
        }

        return false;
    }
}
