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
/// Conditional Value-at-Risk (CVaR) risk measure using the Rockafellar-Uryasev (2000)
/// reformulation:
///
///   CVaR_α(w) = min_ζ { ζ + 1/(S(1−α)) × Σ_s max(−w'r_s − ζ, 0) }
///
/// The auxiliary variable ζ (VaR threshold) is maintained internally and updated
/// via gradient ascent at each evaluation, using the provided learning rate.
///
/// At confidence level α = 0.95 (default), CVaR₉₅ captures the expected loss
/// in the worst 5% of scenarios.
/// </summary>
public sealed class CVaRRiskMeasure : IDownsideRiskMeasure
{
    private readonly decimal _confidenceLevel;
    private decimal _zeta;

    /// <summary>
    /// Initializes a new instance of the <see cref="CVaRRiskMeasure"/> class.
    /// </summary>
    /// <param name="confidenceLevel">CVaR confidence level α. Default 0.95.</param>
    public CVaRRiskMeasure(decimal confidenceLevel = 0.95m)
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
    /// Resets the internal VaR threshold (ζ) to its initial value.
    /// Call this before each independent optimization run to prevent
    /// state leakage between runs when the instance is shared (e.g., via DI singleton).
    /// </summary>
    public void Reset() => _zeta = 0m;

    /// <inheritdoc />
    public (decimal Value, decimal[] Gradient) Evaluate(
        decimal[] weights,
        decimal[][] scenarios,
        decimal learningRate)
    {
        var n = weights.Length;
        var s = scenarios.Length;

        if (s == 0)
        {
            throw new Boutquin.Trading.Domain.Exceptions.CalculationException(
                "At least one scenario is required for CVaR evaluation.");
        }

        var tailFactor = 1m / (s * (1m - _confidenceLevel));
        var tailCount = Math.Max(1, (int)(s * (1m - _confidenceLevel)));

        // --- Step 1: Compute portfolio returns for all scenarios ---
        var portReturns = new decimal[s];
        for (var t = 0; t < s; t++)
        {
            for (var i = 0; i < n; i++)
            {
                portReturns[t] += weights[i] * scenarios[t][i];
            }
        }

        // --- Step 2: Set ζ analytically to the empirical VaR ---
        // The R-U reformulation: CVaR(w) = min_ζ { ζ + 1/(S(1−α)) × Σ max(−w'r − ζ, 0) }
        // The minimizer ζ* equals VaR_α, the (1−α)-quantile of portfolio losses.
        // Computing this exactly eliminates the gradient-descent ζ update, which
        // suffered from learning-rate sensitivity and state corruption during
        // line-search evaluations. Because ζ is now a pure function of weights,
        // there is no accumulated state — evaluation order doesn't matter.
        var sorted = new decimal[s];
        Array.Copy(portReturns, sorted, s);
        Array.Sort(sorted); // ascending: worst returns first

        // Set ζ just below the tail boundary so that tail scenarios satisfy
        // the strict inequality (loss > 0). Using sorted[tailCount] (the best
        // non-tail return) ensures all tailCount scenarios are counted, even
        // when tail returns are tied at exactly the VaR threshold.
        _zeta = -sorted[Math.Min(tailCount, s - 1)];

        // --- Step 3: Compute CVaR value and weight gradients ---
        var cvarSum = 0m;
        var gradW = new decimal[n];

        for (var t = 0; t < s; t++)
        {
            // Loss exceedance: max(−w'r_t − ζ, 0)
            var loss = -portReturns[t] - _zeta;
            if (loss > 0m)
            {
                cvarSum += loss;

                // ∂/∂w of max(−w'r_t − ζ, 0) = −r_t when active
                for (var i = 0; i < n; i++)
                {
                    gradW[i] += tailFactor * (-scenarios[t][i]);
                }
            }
        }

        var cvar = _zeta + tailFactor * cvarSum;

        return (cvar, gradW);
    }
}
