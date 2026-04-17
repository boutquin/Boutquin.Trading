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
/// Compares the throughput of all five covariance estimators across asset universe sizes.
/// Each benchmark estimates an NxN covariance matrix from T=252 daily return observations.
/// </summary>
[MemoryDiagnoser]
public class CovarianceEstimatorBenchmarks
{
    private const int T = 252;

    [Params(3, 5, 10)]
    public int N { get; set; }

    private decimal[][] _returns = null!;

    [GlobalSetup]
    public void Setup() =>
        _returns = BenchmarkHelpers.GenerateReturns(N, T, annualVol: 0.15, seed: 42);

    [Benchmark(Baseline = true)]
    public decimal[,] Sample() =>
        new SampleCovarianceEstimator().Estimate(_returns);

    [Benchmark]
    public decimal[,] Ewma() =>
        new ExponentiallyWeightedCovarianceEstimator(lambda: 0.94m).Estimate(_returns);

    [Benchmark]
    public decimal[,] LedoitWolfConstantCorrelation() =>
        new LedoitWolfConstantCorrelationEstimator().Estimate(_returns);

    [Benchmark]
    public decimal[,] QuadraticInverseShrinkage() =>
        new QuadraticInverseShrinkageEstimator().Estimate(_returns);

    [Benchmark]
    public decimal[,] DenoisedRmt() =>
        new DenoisedCovarianceEstimator().Estimate(_returns);
}
