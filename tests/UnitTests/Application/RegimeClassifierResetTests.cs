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

public sealed class RegimeClassifierResetTests
{
    [Fact]
    public void Reset_ClearsPriorRegime_NextCallUsesNoHistory()
    {
        var classifier = new GrowthInflationRegimeClassifier(deadband: 0.5m);

        // First call: clear signals → sets prior to RisingGrowthRisingInflation
        classifier.Classify(1.0m, 1.0m);

        // Second call: ambiguous signals within deadband → uses prior (RisingGrowthRisingInflation)
        var beforeReset = classifier.Classify(0.0m, 0.0m);
        beforeReset.Should().Be(EconomicRegime.RisingGrowthRisingInflation,
            "ambiguous signals should use prior regime");

        // Reset clears the prior
        classifier.Reset();

        // Third call: ambiguous signals, no prior → should default to FallingGrowthFallingInflation
        var afterReset = classifier.Classify(0.0m, 0.0m);
        afterReset.Should().Be(EconomicRegime.FallingGrowthFallingInflation,
            "after reset, ambiguous signals with no prior should default to FallingGrowthFallingInflation");
    }
}
