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

namespace Boutquin.Trading.Domain.Analytics;

/// <summary>
/// Result of a Monte Carlo bootstrap simulation over portfolio returns.
/// </summary>
/// <param name="SimulationCount">Number of bootstrap simulations run.</param>
/// <param name="SharpeRatios">Sharpe ratios from each simulation, sorted ascending. This is a defensive copy; callers should not mutate it.</param>
/// <param name="MedianSharpe">Median Sharpe ratio across simulations.</param>
/// <param name="Percentile5Sharpe">5th percentile Sharpe ratio (worst-case).</param>
/// <param name="Percentile95Sharpe">95th percentile Sharpe ratio (best-case).</param>
/// <param name="MeanSharpe">Mean Sharpe ratio across simulations.</param>
public sealed record MonteCarloResult(
    int SimulationCount,
    decimal[] SharpeRatios,
    decimal MedianSharpe,
    decimal Percentile5Sharpe,
    decimal Percentile95Sharpe,
    decimal MeanSharpe);
