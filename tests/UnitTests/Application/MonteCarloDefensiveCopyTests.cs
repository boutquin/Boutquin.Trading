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

using Boutquin.Trading.Application.Analytics;

public sealed class MonteCarloDefensiveCopyTests
{
    [Fact]
    public void Run_MutatingReturnedArray_DoesNotAffectResult()
    {
        var dailyReturns = new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, 0.00m, -0.01m, 0.02m };
        var simulator = new MonteCarloSimulator(simulationCount: 50, seed: 42);

        var result1 = simulator.Run(dailyReturns);
        var originalFirstSharpe = result1.SharpeRatios[0];

        // Mutate the returned array
        result1.SharpeRatios[0] = 999.99m;

        // Run again with same seed — should produce the same original result
        var result2 = simulator.Run(dailyReturns);

        result2.SharpeRatios[0].Should().Be(originalFirstSharpe,
            "mutating the returned SharpeRatios array should not affect subsequent runs");
    }
}
