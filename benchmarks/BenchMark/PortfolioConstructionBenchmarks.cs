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

namespace Boutquin.Trading.BenchMark;
/// <summary>
/// Compares the throughput of five portfolio construction models across asset universe sizes.
/// Each benchmark calls <c>ComputeTargetWeights</c> once with N assets and T=252 observations.
/// Covariance estimation is included in the measurement as it is part of the real call path.
/// </summary>
[MemoryDiagnoser]
public class PortfolioConstructionBenchmarks
{
    private const int T = 252;

    [Params(3, 5, 10)]
    public int N { get; set; }

    private Symbol[] _assets = null!;
    private decimal[][] _returns = null!;

    [GlobalSetup]
    public void Setup()
    {
        _assets = [.. Enumerable.Range(0, N).Select(i => new Symbol($"A{i:00}"))];
        _returns = BenchmarkHelpers.GenerateReturns(N, T, annualVol: 0.15, seed: 42);
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyDictionary<Symbol, decimal> EqualWeight() =>
        new EqualWeightConstruction().ComputeTargetWeights(_assets, _returns);

    [Benchmark]
    public IReadOnlyDictionary<Symbol, decimal> InverseVolatility() =>
        new InverseVolatilityConstruction().ComputeTargetWeights(_assets, _returns);

    [Benchmark]
    public IReadOnlyDictionary<Symbol, decimal> MinimumVariance() =>
        new MinimumVarianceConstruction(new LedoitWolfConstantCorrelationEstimator())
            .ComputeTargetWeights(_assets, _returns);

    [Benchmark]
    public IReadOnlyDictionary<Symbol, decimal> RiskParity() =>
        new RiskParityConstruction(new LedoitWolfConstantCorrelationEstimator())
            .ComputeTargetWeights(_assets, _returns);

    [Benchmark]
    public IReadOnlyDictionary<Symbol, decimal> HierarchicalRiskParity() =>
        new HierarchicalRiskParityConstruction(new LedoitWolfConstantCorrelationEstimator())
            .ComputeTargetWeights(_assets, _returns);
}
