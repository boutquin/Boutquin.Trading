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
    public void Run_ReturnedSharpeRatios_IsReadOnly()
    {
        var dailyReturns = new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, 0.00m, -0.01m, 0.02m };
        var simulator = new MonteCarloSimulator(simulationCount: 50, seed: 42);

        var result = simulator.Run(dailyReturns);

        // SharpeRatios is IReadOnlyList<decimal> — cannot be mutated
        result.SharpeRatios.Should().BeAssignableTo<IReadOnlyList<decimal>>();
        result.SharpeRatios.Count.Should().Be(50);
    }

    [Fact]
    public void Run_DeterministicSeed_ProducesConsistentResults()
    {
        var dailyReturns = new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, 0.00m, -0.01m, 0.02m };

        var result1 = new MonteCarloSimulator(simulationCount: 50, seed: 42).Run(dailyReturns);
        var result2 = new MonteCarloSimulator(simulationCount: 50, seed: 42).Run(dailyReturns);

        result1.SharpeRatios[0].Should().Be(result2.SharpeRatios[0],
            "same seed should produce identical results across runs");
    }
}
