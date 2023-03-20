// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using BenchmarkDotNet.Attributes;
using Boutquin.Trading.Domain.Extensions;

namespace Boutquin.Trading.BenchMark;


/// <summary>
/// Benchmark class to measure the performance of the DecimalArrayExtensions's AnnualizedSharpeRatio method.
/// </summary>
[MemoryDiagnoser]
public class CalculationBenchmark
{
    private decimal[] _dailyReturns = Array.Empty<decimal>();
    private decimal _riskFreeRate = 0m;

    /// <summary>
    /// Sets up the benchmark by initializing the daily returns, and risk-free rate.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Define sample daily returns for the benchmark.
        _dailyReturns = new[] { 0.01m, -0.02m, 0.03m, -0.04m, 0.05m, -0.06m, 0.07m, -0.08m };

        // Define the risk-free rate for the benchmark.
        _riskFreeRate = 0.02m;
    }

    /// <summary>
    /// Benchmark method for the CalculateSharpeRatio method in the Portfolio class.
    /// </summary>
    [Benchmark]
    public void CalculateSharpeRatio() =>
        // Call the CalculateSharpeRatio method with the sample data.
        _dailyReturns.AnnualizedSharpeRatio(_riskFreeRate);
}
