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

namespace Boutquin.Trading.Application.Regime;

using Boutquin.Trading.Domain.Enums;

/// <summary>
/// Classifies the current economic regime based on growth and inflation signals using a four-quadrant model with configurable deadband hysteresis.
/// </summary>
public sealed class GrowthInflationRegimeClassifier : IRegimeClassifier
{
    private readonly decimal _deadband;
    private EconomicRegime? _priorRegime;

    /// <summary>Initializes a new instance with the specified deadband for hysteresis.</summary>
    /// <param name="deadband">The deadband threshold below which signals are considered ambiguous.</param>
    public GrowthInflationRegimeClassifier(decimal deadband = 0m)
    {
        if (deadband < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(deadband), "Deadband must be non-negative.");
        }

        _deadband = deadband;
    }

    /// <summary>
    /// Clears the cached prior regime, so the next classification uses no history.
    /// </summary>
    public void Reset() => _priorRegime = null;

    /// <inheritdoc/>
    public EconomicRegime Classify(decimal growthSignal, decimal inflationSignal)
    {
        var growthRising = growthSignal > _deadband;
        var growthFalling = growthSignal < -_deadband;
        var inflationRising = inflationSignal > _deadband;
        var inflationFalling = inflationSignal < -_deadband;

        // If both signals are ambiguous (within deadband), use prior regime
        if (!growthRising && !growthFalling && !inflationRising && !inflationFalling && _priorRegime.HasValue)
        {
            return _priorRegime.Value;
        }

        // If one signal is ambiguous, use prior regime's value for that dimension
        var isGrowthRising = growthRising || (!growthFalling && _priorRegime.HasValue &&
            (_priorRegime.Value == EconomicRegime.RisingGrowthRisingInflation ||
             _priorRegime.Value == EconomicRegime.RisingGrowthFallingInflation));

        var isInflationRising = inflationRising || (!inflationFalling && _priorRegime.HasValue &&
            (_priorRegime.Value == EconomicRegime.RisingGrowthRisingInflation ||
             _priorRegime.Value == EconomicRegime.FallingGrowthRisingInflation));

        var regime = (isGrowthRising, isInflationRising) switch
        {
            (true, true) => EconomicRegime.RisingGrowthRisingInflation,
            (true, false) => EconomicRegime.RisingGrowthFallingInflation,
            (false, true) => EconomicRegime.FallingGrowthRisingInflation,
            (false, false) => EconomicRegime.FallingGrowthFallingInflation,
        };

        _priorRegime = regime;
        return regime;
    }
}
