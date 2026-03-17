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

namespace Boutquin.Trading.Tests.UnitTests.Application;

using Boutquin.Trading.Application.Regime;
using Boutquin.Trading.Domain.Enums;

public sealed class RegimeClassifierTests
{
    // ============================================================
    // GrowthInflationRegimeClassifier Tests (RP4-03)
    // ============================================================

    [Theory]
    [InlineData(1.0, 1.0, EconomicRegime.RisingGrowthRisingInflation)]
    [InlineData(1.0, -1.0, EconomicRegime.RisingGrowthFallingInflation)]
    [InlineData(-1.0, 1.0, EconomicRegime.FallingGrowthRisingInflation)]
    [InlineData(-1.0, -1.0, EconomicRegime.FallingGrowthFallingInflation)]
    public void Classify_KnownSignals_ShouldMapCorrectly(
        double growth, double inflation, EconomicRegime expected)
    {
        var classifier = new GrowthInflationRegimeClassifier();
        classifier.Classify((decimal)growth, (decimal)inflation).Should().Be(expected);
    }

    [Fact]
    public void Classify_AmbiguousSignals_ShouldUsePriorRegime()
    {
        var classifier = new GrowthInflationRegimeClassifier(deadband: 0.5m);

        // First clear signal
        classifier.Classify(1.0m, 1.0m).Should().Be(EconomicRegime.RisingGrowthRisingInflation);

        // Ambiguous signal (within deadband) → should use prior
        classifier.Classify(0.1m, 0.1m).Should().Be(EconomicRegime.RisingGrowthRisingInflation);
    }

    [Fact]
    public void Classify_RegimeChangeTriggersOnClearSignal()
    {
        var classifier = new GrowthInflationRegimeClassifier(deadband: 0.1m);

        classifier.Classify(1.0m, 1.0m).Should().Be(EconomicRegime.RisingGrowthRisingInflation);
        classifier.Classify(-1.0m, -1.0m).Should().Be(EconomicRegime.FallingGrowthFallingInflation);
    }

    [Fact]
    public void Classify_NoDeadband_ShouldUseZeroBoundary()
    {
        var classifier = new GrowthInflationRegimeClassifier(deadband: 0m);

        // Zero values → both false → FallingGrowthFallingInflation (since 0 is not > 0)
        classifier.Classify(0m, 0m).Should().Be(EconomicRegime.FallingGrowthFallingInflation);
    }

    [Fact]
    public void Classify_NegativeDeadband_ShouldThrow()
    {
        var act = () => new GrowthInflationRegimeClassifier(deadband: -0.1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
