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
/// Conditional Drawdown at Risk (CDaR) risk measure using additive drawdowns
/// (Chekhlov, Uryasev, Zabarankin 2005).
///
/// Analogous to CVaR but applied to the drawdown series instead of returns:
///
///   CDaR_α(w) = ζ + 1/(T(1−α)) × Σ_t max(−dd_t − ζ, 0)
///
/// where dd_t = C_t − P_t is the additive drawdown at time t (C_t = cumulative
/// return, P_t = running peak of cumulative returns).
///
/// The auxiliary variable ζ (drawdown VaR threshold) is set analytically to the
/// empirical drawdown quantile at each evaluation, matching the CVaR pattern.
///
/// At confidence level α = 0.95 (default), CDaR₉₅ captures the expected drawdown
/// in the worst 5% of drawdown observations.
/// </summary>
public sealed class CDaRRiskMeasure : IDownsideRiskMeasure
{
    private readonly decimal _confidenceLevel;
    private decimal _zeta;

    /// <summary>
    /// Initializes a new instance of the <see cref="CDaRRiskMeasure"/> class.
    /// </summary>
    /// <param name="confidenceLevel">CDaR confidence level α. Default 0.95.</param>
    public CDaRRiskMeasure(decimal confidenceLevel = 0.95m)
    {
        if (confidenceLevel is <= 0m or >= 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceLevel),
                confidenceLevel,
                "Confidence level must be between 0 and 1 exclusive.");
        }

        _confidenceLevel = confidenceLevel;
        _zeta = 0m;
    }

    /// <summary>
    /// Resets the internal drawdown VaR threshold (ζ) to its initial value.
    /// </summary>
    public void Reset() => _zeta = 0m;

    /// <inheritdoc />
    public (decimal Value, decimal[] Gradient) Evaluate(
        decimal[] weights,
        decimal[][] scenarios,
        decimal learningRate)
    {
        var n = weights.Length;
        var t = scenarios.Length;

        if (t == 0)
        {
            throw new Boutquin.Trading.Domain.Exceptions.CalculationException(
                "At least one scenario is required for CDaR evaluation.");
        }

        var tailFactor = 1m / (t * (1m - _confidenceLevel));
        var tailCount = Math.Max(1, (int)(t * (1m - _confidenceLevel)));

        // --- Step 1: Compute portfolio returns and cumulative sums ---
        var portReturns = new decimal[t];
        for (var s = 0; s < t; s++)
        {
            for (var i = 0; i < n; i++)
            {
                portReturns[s] += weights[i] * scenarios[s][i];
            }
        }

        // --- Step 2: Compute additive cumulative returns, running peak, and drawdowns ---
        var cumReturn = new decimal[t];
        var peak = new decimal[t];
        var peakIdx = new int[t];
        var drawdown = new decimal[t];

        cumReturn[0] = portReturns[0];
        peak[0] = Math.Max(0m, cumReturn[0]);
        peakIdx[0] = cumReturn[0] >= 0m ? 0 : -1; // -1 means peak is at the origin (0)
        drawdown[0] = cumReturn[0] - peak[0];

        for (var s = 1; s < t; s++)
        {
            cumReturn[s] = cumReturn[s - 1] + portReturns[s];

            if (cumReturn[s] >= peak[s - 1])
            {
                peak[s] = cumReturn[s];
                peakIdx[s] = s;
            }
            else
            {
                peak[s] = peak[s - 1];
                peakIdx[s] = peakIdx[s - 1];
            }

            drawdown[s] = cumReturn[s] - peak[s];
        }

        // --- Step 3: Set ζ analytically to the empirical drawdown VaR ---
        // Sort drawdowns ascending (most negative first).
        // ζ = -drawdown at the (1-α) quantile boundary.
        var sortedDd = new decimal[t];
        Array.Copy(drawdown, sortedDd, t);
        Array.Sort(sortedDd); // ascending: most negative first

        _zeta = -sortedDd[Math.Min(tailCount, t - 1)];

        // --- Step 4: Compute CDaR value and weight gradients ---
        var cdarSum = 0m;
        var gradW = new decimal[n];

        for (var s = 0; s < t; s++)
        {
            // Drawdown exceedance: max(-dd_t - ζ, 0)
            var exceedance = -drawdown[s] - _zeta;
            if (exceedance > 0m)
            {
                cdarSum += exceedance;

                // Gradient: ∂(-dd_t)/∂w_i = -Σ_{s'=tau+1}^{t} scenarios[s'][i]
                // where tau = peakIdx[s] is when the running peak was achieved.
                // If peakIdx is -1 (peak at origin), sum from s'=0.
                var startIdx = peakIdx[s] >= 0 ? peakIdx[s] + 1 : 0;
                for (var i = 0; i < n; i++)
                {
                    var gradContrib = 0m;
                    for (var sp = startIdx; sp <= s; sp++)
                    {
                        gradContrib -= scenarios[sp][i];
                    }

                    gradW[i] += tailFactor * gradContrib;
                }
            }
        }

        var cdar = _zeta + tailFactor * cdarSum;

        return (cdar, gradW);
    }
}
