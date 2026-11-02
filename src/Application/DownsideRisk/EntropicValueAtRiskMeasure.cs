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
/// Entropic Value-at-Risk (EVaR) risk measure — Ahmadi-Javid (2012), restated and
/// confirmed coherent by Delbaen (2015).
///
/// <para>
/// Finite-sample primal (X = loss = −w′r, J scenarios):
/// </para>
/// <code>
///   EVaR_{1−α}(w) = inf_{z&gt;0} z⁻¹·[ ln( (1/J) Σ_j e^{z·loss_j} ) − ln α ]
/// </code>
/// <para>
/// where the inner parameter <c>α</c> is the <i>tail</i> probability. This class follows
/// the repository's CVaR-style convention where <c>confidenceLevel</c> = 1 − tail (default
/// 0.95), so internally <c>α = 1 − confidenceLevel</c>. Higher confidence ⇒ smaller inner
/// <c>α</c> ⇒ larger (more conservative) EVaR. The limits are
/// <c>EVaR → E[X]</c> as confidence → 0 (<c>α → 1</c>) and <c>EVaR → ess.sup(X)</c> as
/// confidence → 1 (<c>α → 0</c>).
/// </para>
/// <para>
/// The objective is unimodal in <c>z</c> (the moment-generating function is log-convex), so
/// the optimal <c>z*</c> is located by a <b>golden-section search</b> over a fixed bracket.
/// A log-sum-exp shift by <c>max_j(z·loss_j)</c> guards <c>e^{z·loss}</c> against overflow.
/// </para>
/// <para>
/// Gradient at <c>z*</c> (an exponentially-tilted expectation of asset returns):
/// </para>
/// <code>
///   ∂EVaR/∂w_i = Σ_j [ e^{z*·loss_j} / Σ_k e^{z*·loss_k} ]·(−r_{ji})
/// </code>
/// <para>
/// <b>Coherence and ordering.</b> EVaR is coherent (sub-additive, monotone,
/// translation-invariant, positively homogeneous) and convex in <c>w</c>. It is the
/// Chernoff-bound upper envelope of CVaR, so <c>VaR_α ≤ CVaR_α ≤ EVaR_α</c> at the same tail
/// probability — EVaR is the <i>tighter</i> coherent bound (Ahmadi-Javid 2012, Prop. 3.2).
/// </para>
/// <para>
/// <b>Reference:</b> A. Ahmadi-Javid (2012), "Entropic Value-at-Risk: A New Coherent Risk
/// Measure", J. Optimization Theory and Applications 155(3):1105–1123; F. Delbaen (2015),
/// "A Remark on the Structure of Expectiles" / EVaR coherence remark, arXiv:1504.00640.
/// </para>
/// </summary>
public sealed class EntropicValueAtRiskMeasure : IDownsideRiskMeasure
{
    // Golden-section bracket and iteration count. Z_HI is wide enough that the EVaR VALUE
    // has converged for all realistic α (when the worst loss is unique the minimizer can
    // ride the ceiling, but the value asymptotes to ess.sup so the result is stable). These
    // constants are mirrored exactly in tests/Verification/generate_evar_vectors.py so the
    // Python reference vectors agree with this implementation to PrecisionNumeric (1e-6).
    private const double ZLo = 1e-6;
    private const double ZHi = 10000.0;
    private const int GoldenSectionIterations = 200;
    private static readonly double s_invPhi = (Math.Sqrt(5.0) - 1.0) / 2.0;     // 1/φ
    private static readonly double s_invPhi2 = (3.0 - Math.Sqrt(5.0)) / 2.0;    // 1/φ²

    private readonly decimal _confidenceLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntropicValueAtRiskMeasure"/> class.
    /// </summary>
    /// <param name="confidenceLevel">
    /// EVaR confidence level (1 − tail probability). Must be in (0, 1) exclusive. Default 0.95.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="confidenceLevel"/> is not strictly between 0 and 1.
    /// </exception>
    public EntropicValueAtRiskMeasure(decimal confidenceLevel = 0.95m)
    {
        if (confidenceLevel is <= 0m or >= 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceLevel),
                confidenceLevel,
                "Confidence level must be between 0 and 1 exclusive.");
        }

        _confidenceLevel = confidenceLevel;
    }

    /// <summary>
    /// Resets internal state. EVaR holds no accumulated auxiliary state (the minimizer
    /// <c>z*</c> is recomputed from the weights at every evaluation), so this is a no-op
    /// provided for parity with the other stateful <see cref="IDownsideRiskMeasure"/>
    /// implementations.
    /// </summary>
    public void Reset()
    {
        // No accumulated state to reset — z* is a pure function of the current weights.
    }

    /// <inheritdoc />
    public (decimal Value, decimal[] Gradient) Evaluate(
        decimal[] weights,
        decimal[][] scenarios,
        decimal learningRate)
    {
        var n = weights.Length;
        var j = scenarios.Length;

        if (j == 0)
        {
            throw new Boutquin.Trading.Domain.Exceptions.CalculationException(
                "At least one scenario is required for EVaR evaluation.");
        }

        // --- Step 1: Losses are negative portfolio returns (computed in double for the
        // exponential arithmetic that follows). ---
        var losses = new double[j];
        for (var t = 0; t < j; t++)
        {
            var port = 0.0;
            for (var i = 0; i < n; i++)
            {
                port += (double)weights[i] * (double)scenarios[t][i];
            }

            losses[t] = -port;
        }

        // Inner tail probability α = 1 − confidence (see class remarks).
        var lnAlpha = Math.Log(1.0 - (double)_confidenceLevel);

        // --- Step 2: Golden-section minimization of g(z) over [ZLo, ZHi] ---
        // g is unimodal (log-convex MGF), so golden-section converges to the minimizer.
        var a = ZLo;
        var b = ZHi;
        var c = a + (s_invPhi2 * (b - a));
        var d = a + (s_invPhi * (b - a));
        var fc = Objective(c, losses, lnAlpha);
        var fd = Objective(d, losses, lnAlpha);

        for (var iter = 0; iter < GoldenSectionIterations; iter++)
        {
            if (fc < fd)
            {
                b = d;
                d = c;
                fd = fc;
                c = a + (s_invPhi2 * (b - a));
                fc = Objective(c, losses, lnAlpha);
            }
            else
            {
                a = c;
                c = d;
                fc = fd;
                d = a + (s_invPhi * (b - a));
                fd = Objective(d, losses, lnAlpha);
            }
        }

        var zStar = (a + b) / 2.0;
        var value = Objective(zStar, losses, lnAlpha);

        // --- Step 3: Gradient — exponentially-tilted expectation of −returns at z*. ---
        // Log-sum-exp shift by the max exponent guards against overflow.
        var shift = double.NegativeInfinity;
        for (var t = 0; t < j; t++)
        {
            var e = zStar * losses[t];
            if (e > shift)
            {
                shift = e;
            }
        }

        var tilt = new double[j];
        var tiltSum = 0.0;
        for (var t = 0; t < j; t++)
        {
            tilt[t] = Math.Exp((zStar * losses[t]) - shift);
            tiltSum += tilt[t];
        }

        var gradW = new decimal[n];
        for (var t = 0; t < j; t++)
        {
            var p = tilt[t] / tiltSum;
            for (var i = 0; i < n; i++)
            {
                gradW[i] += (decimal)(p * -(double)scenarios[t][i]);
            }
        }

        return ((decimal)value, gradW);
    }

    /// <summary>
    /// Evaluates the EVaR primal objective
    /// <c>z⁻¹·[ ln( (1/J) Σ_j e^{z·loss_j} ) − ln α ]</c> at a candidate <c>z</c>,
    /// using a log-sum-exp shift for numerical stability.
    /// </summary>
    private static double Objective(double z, double[] losses, double lnAlpha)
    {
        var shift = double.NegativeInfinity;
        for (var t = 0; t < losses.Length; t++)
        {
            var e = z * losses[t];
            if (e > shift)
            {
                shift = e;
            }
        }

        var sumExp = 0.0;
        for (var t = 0; t < losses.Length; t++)
        {
            sumExp += Math.Exp((z * losses[t]) - shift);
        }

        var logMeanMgf = shift + Math.Log(sumExp / losses.Length);
        return (logMeanMgf - lnAlpha) / z;
    }
}
