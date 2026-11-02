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

namespace Boutquin.Trading.Application.ExpectedReturns;

/// <summary>
/// The behavior-preserving baseline expected-return estimator: <c>E[r_i]</c> is the sample mean of
/// <c>returns[i]</c>. This is a byte-identical extraction of the <c>returns[i].Average()</c> computation
/// that <c>MeanVarianceConstruction</c> and <c>MeanDownsideRiskConstruction</c> previously performed inline,
/// so wiring an <see cref="IExpectedReturnEstimator"/> into those models with this implementation preserves
/// their output exactly (the byte-identity equivalence contract).
/// </summary>
/// <remarks>
/// <para>Ilmanen (2012, Introduction, pp. 4–5) characterizes "using the historical average return as the
/// best estimate of future return" as the rear-view baseline that forward-looking anchors
/// (<see cref="YieldAnchoredExpectedReturnEstimator"/>) are designed to improve on. This estimator ships as
/// that baseline — the default that keeps existing blueprints unchanged.</para>
/// <para><b>Auxiliary series ignored:</b> this estimator is a pure function of <c>returns</c> and does not
/// read <c>auxiliarySeries</c>.</para>
/// <para><b>Thread-safety:</b> stateless and immutable; a single instance may be used concurrently.</para>
/// </remarks>
public sealed class HistoricalMeanExpectedReturnEstimator : IExpectedReturnEstimator
{
    /// <inheritdoc />
    public decimal[] EstimateExpectedReturns(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null)
    {
        Guard.AgainstNull(() => assets);

        var n = assets.Count;
        if (n == 0)
        {
            return [];
        }

        if (returns is null || returns.Length != n)
        {
            throw new ArgumentException("Returns array must have one series per asset.", nameof(returns));
        }

        var means = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            if (returns[i] is null || returns[i].Length == 0)
            {
                throw new ArgumentException(
                    $"Return series for asset index {i} must be non-null and non-empty.", nameof(returns));
            }

            means[i] = returns[i].Average();
        }

        return means;
    }
}
