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

namespace Boutquin.Trading.Application.DownsideRisk;

/// <summary>
/// Downside deviation risk measure for Sortino-style optimization:
///
///   DownsideDeviation(w) = sqrt(1/S × Σ_s min(w'r_s − MAR, 0)²)
///
/// where MAR is the minimum acceptable return (default 0).
///
/// When used in <c>MeanDownsideRiskConstruction</c> with riskAversion = 1,
/// the optimizer effectively maximizes the Sortino ratio:
///   (E[r] − MAR) / DownsideDeviation(w)
///
/// The gradient is computed analytically. When downside deviation is zero
/// (no scenarios below MAR), the gradient returns zero — the construction
/// model's expected-return gradient will then dominate, pushing toward
/// the highest-return portfolio.
/// </summary>
public sealed class DownsideDeviationRiskMeasure : IDownsideRiskMeasure
{
    private readonly decimal _minimumAcceptableReturn;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownsideDeviationRiskMeasure"/> class.
    /// </summary>
    /// <param name="minimumAcceptableReturn">Minimum acceptable return (MAR). Default 0.</param>
    public DownsideDeviationRiskMeasure(decimal minimumAcceptableReturn = 0m)
    {
        _minimumAcceptableReturn = minimumAcceptableReturn;
    }

    /// <inheritdoc />
    public (decimal Value, decimal[] Gradient) Evaluate(
        decimal[] weights,
        decimal[][] scenarios,
        decimal learningRate)
    {
        var n = weights.Length;
        var s = scenarios.Length;

        var downsideSumSq = 0m;
        var gradSumSq = new decimal[n];

        for (var t = 0; t < s; t++)
        {
            // Portfolio return for scenario t
            var portReturnT = 0m;
            for (var i = 0; i < n; i++)
            {
                portReturnT += weights[i] * scenarios[t][i];
            }

            var shortfall = _minimumAcceptableReturn - portReturnT;
            if (shortfall > 0m)
            {
                downsideSumSq += shortfall * shortfall;

                // d/dw of (MAR − w'r_t)² = 2(MAR − w'r_t)(−r_t) when shortfall > 0
                for (var i = 0; i < n; i++)
                {
                    gradSumSq[i] += 2m * shortfall * (-scenarios[t][i]);
                }
            }
        }

        var downsideVariance = downsideSumSq / s;

        if (downsideVariance <= 0m)
        {
            // No downside risk — gradient is zero (no risk to penalize)
            return (0m, new decimal[n]);
        }

        var downsideDev = (decimal)Math.Sqrt((double)downsideVariance);
        if (downsideDev <= 0m)
        {
            return (0m, new decimal[n]);
        }

        // d(downsideDev)/dw = d(sqrt(downsideSumSq/S))/dw
        //                   = 1/(2*S*downsideDev) × d(downsideSumSq)/dw
        var grad = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            grad[i] = gradSumSq[i] / (2m * s * downsideDev);
        }

        return (downsideDev, grad);
    }
}
