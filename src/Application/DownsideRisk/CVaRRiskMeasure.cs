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

        // --- Step 2: Set ζ to the R-U minimizer over the candidate order statistics ---
        // R-U reformulation: CVaR(w) = min_ζ { ζ + 1/(S(1−α)) × Σ max(−w'r − ζ, 0) }.
        // The objective is convex and piecewise-linear in ζ with kinks at ζ = −r_s, so
        // its minimum is attained at one of the sorted-return order statistics.
        //
        // In the integer-tail regime S·(1−α) ≥ 1 (the dominant case for realistic
        // scenario counts at α ≥ 0.9) the empirical-quantile ζ at index
        // min(tailCount, s−1) is PROVABLY the R-U minimizer, so we assign it directly —
        // an O(S log S) fast path (sort only), avoiding the O(S²) candidate scan. Only
        // in the S·(1−α) < 1 small-S regime — where tailFactor = 1/(S(1−α)) > 1
        // over-weights the worst exceedance so the empirical quantile sits off the
        // minimizer — do we run the full min-over-ζ scan and pick the true minimizing
        // order statistic, restoring the correct CVaR (which degenerates to the worst
        // single loss). Both regimes keep ζ a pure function of weights (no accumulated
        // state) and integer-tail values AND gradients byte-identical to the prior path.
        var sorted = new decimal[s];
        Array.Copy(portReturns, sorted, s);
        Array.Sort(sorted); // ascending: worst returns first

        var quantileZeta = -sorted[Math.Min(tailCount, s - 1)];
        if (s * (1m - _confidenceLevel) >= 1m)
        {
            // Integer-tail regime: the empirical quantile is the minimizer — assign it
            // directly (identical value AND active set as the full scan would yield).
            _zeta = quantileZeta;
        }
        else
        {
            // S·(1−α) < 1 regime: run the full min-over-ζ scan, then pick the minimizing
            // order statistic (smallest such ζ — the largest k — i.e. the canonical R-U
            // VaR among the minimizers).
            var minObjective = decimal.MaxValue;
            for (var k = 0; k < s; k++)
            {
                var objective = RuObjective(-sorted[k], portReturns, tailFactor);
                if (objective < minObjective)
                {
                    minObjective = objective;
                }
            }

            _zeta = quantileZeta;
            for (var k = s - 1; k >= 0; k--)
            {
                if (RuObjective(-sorted[k], portReturns, tailFactor) <= minObjective)
                {
                    _zeta = -sorted[k];
                    break;
                }
            }
        }

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

    /// <summary>
    /// Evaluates the Rockafellar-Uryasev objective
    /// <c>ζ + tailFactor × Σ_t max(−r_t − ζ, 0)</c> at a candidate ζ.
    /// </summary>
    private static decimal RuObjective(decimal zeta, decimal[] portReturns, decimal tailFactor)
    {
        var exceedanceSum = 0m;
        for (var t = 0; t < portReturns.Length; t++)
        {
            var exceedance = -portReturns[t] - zeta;
            if (exceedance > 0m)
            {
                exceedanceSum += exceedance;
            }
        }

        return zeta + tailFactor * exceedanceSum;
    }
}
