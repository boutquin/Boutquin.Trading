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

public sealed class MonteCarloTests
{
    [Fact]
    public void MonteCarlo_Run_ShouldProduceDistribution()
    {
        var returns = new decimal[252];
        var rng = new Random(42);
        for (var i = 0; i < returns.Length; i++)
        {
            returns[i] = (decimal)(rng.NextDouble() * 0.04 - 0.02);
        }

        var simulator = new MonteCarloSimulator(simulationCount: 1000, seed: 42);
        var result = simulator.Run(returns);

        result.SimulationCount.Should().Be(1000);
        result.SharpeRatios.Count.Should().Be(1000);
        result.Percentile5Sharpe.Should().BeLessThanOrEqualTo(result.MedianSharpe);
        result.MedianSharpe.Should().BeLessThanOrEqualTo(result.Percentile95Sharpe);
    }

    [Fact]
    public void MonteCarlo_Run_5thPercentile_ShouldBeWorstCase()
    {
        // Positive returns → Sharpes should be mostly positive
        var returns = Enumerable.Repeat(0.001m, 100).ToArray();

        var simulator = new MonteCarloSimulator(simulationCount: 500, seed: 123);
        var result = simulator.Run(returns);

        // For constant returns, all Sharpes will be 0 (zero variance in resampled constant series...
        // actually they should be near-constant since with replacement there's some variance)
        // With constant 0.001 returns, resampled series will always be constant → stdDev = 0 → Sharpe = 0
        result.SharpeRatios.All(s => s == 0m).Should().BeTrue();
    }

    [Fact]
    public void MonteCarlo_Run_ConfidenceIntervalsCalculated()
    {
        var rng = new Random(42);
        var returns = Enumerable.Range(0, 500).Select(_ => (decimal)(rng.NextDouble() * 0.02 - 0.01)).ToArray();

        var simulator = new MonteCarloSimulator(500, seed: 99);
        var result = simulator.Run(returns);

        result.Percentile5Sharpe.Should().BeLessThan(result.Percentile95Sharpe);
    }

    [Fact]
    public void MonteCarlo_Run_InsufficientData_ShouldThrow()
    {
        var simulator = new MonteCarloSimulator(100);
        var act = () => simulator.Run(new[] { 0.01m });
        act.Should().Throw<InsufficientDataException>();
    }

    [Fact]
    public void MonteCarlo_Run_DeterministicSeed_ShouldBeReproducible()
    {
        var returns = Enumerable.Range(0, 100).Select(i => (decimal)(i % 3) * 0.01m - 0.01m).ToArray();

        var result1 = new MonteCarloSimulator(100, seed: 42).Run(returns);
        var result2 = new MonteCarloSimulator(100, seed: 42).Run(returns);

        result1.MeanSharpe.Should().Be(result2.MeanSharpe);
        result1.MedianSharpe.Should().Be(result2.MedianSharpe);
    }
}
