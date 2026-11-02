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

namespace Boutquin.Trading.Application.Analytics;

using Boutquin.Numerics.Distributions;
using Boutquin.Trading.Domain.Extensions;

/// <summary>
/// Computes the <b>Deflated Sharpe Ratio (DSR)</b> — the Probabilistic Sharpe Ratio evaluated
/// against a selection-bias-corrected benchmark that accounts for the number of independent
/// backtest configurations tried.
/// </summary>
/// <remarks>
/// <para>
/// Reference: Bailey, D. H. &amp; López de Prado, M. (2014), <i>"The Deflated Sharpe Ratio:
/// Correcting for Selection Bias, Backtest Overfitting, and Non-Normality"</i>,
/// Journal of Portfolio Management 40(5):94–107. The DSR is the paper's Eq. (2) with the
/// benchmark set to the expected maximum Sharpe ratio across <c>N</c> trials under the null:
/// </para>
/// <code>
/// ŜR₀ = √V[{ŜR_n}] · ( (1−γ)·Z⁻¹[1−1/N] + γ·Z⁻¹[1−1/(N·e)] )
/// DSR = PSR(SR* = ŜR₀)
/// </code>
/// <para>
/// where <c>γ ≈ 0.5772156649</c> is the Euler–Mascheroni constant, <c>e</c> is Euler's number,
/// <c>Z⁻¹[·]</c> is the standard-normal inverse CDF, and <c>V[{ŜR_n}]</c> is the variance across
/// the <c>N</c> trial-Sharpe estimates. This is the zero-mean (<c>H₀: SR = 0</c>) special case
/// of the paper's general expected-maximum formula (Eq. 1), confirmed byte-for-byte by the
/// paper's Python "Snippet 1" (Appendix A.2). The V1 form assumes <b>independent</b> trials
/// (the paper's Appendix 3 non-independent correction is intentionally out of scope).
/// </para>
/// <para>
/// <b>Kurtosis convention (normative).</b> As with <see cref="ProbabilisticSharpeRatio"/>, the
/// <c>kurtosis</c> parameter of <see cref="Evaluate"/> is the <b>full</b> (non-excess) kurtosis
/// (Gaussian = 3). <see cref="FromReturns"/> derives moments directly from a return array and
/// therefore converts <c>fullKurtosis = returns.Kurtosis() + 3m</c> at the boundary, because
/// <see cref="DecimalArrayExtensions.Kurtosis"/> returns <i>excess</i> (Fisher) kurtosis.
/// Omitting the <c>+3</c> understates the denominator and silently <i>inflates</i> the DSR —
/// the opposite of the metric's purpose. The paper's worked example reproduces
/// <c>DSR ≈ 0.9004</c> only with this conversion.
/// </para>
/// <para>
/// <b>Annualization.</b> <see cref="Evaluate"/> and <see cref="FromReturns"/> accept an
/// <c>observedSharpe</c> and <c>varianceOfTrialSharpes</c> expressed at the annualization implied
/// by <c>tradingDaysPerYear</c> (default 1 = already per-period). Both are internally converted to
/// per-period units — <c>ŜR / √f</c> and <c>V / f</c> — before entering the per-period PSR, which
/// is where the paper's Eq. (2) is defined. The paper's worked example uses
/// <c>tradingDaysPerYear = 250</c>, annualized <c>ŜR = 2.5</c>, and annualized <c>V = 0.5</c>.
/// </para>
/// </remarks>
public static class DeflatedSharpeRatio
{
    private const double EulerMascheroni = 0.5772156649d;

    /// <summary>
    /// Computes the expected maximum Sharpe ratio under the null across <paramref name="numTrials"/>
    /// independent trials — the DSR benchmark <c>ŜR₀</c> (Bailey &amp; López de Prado 2014, Snippet 1).
    /// </summary>
    /// <param name="numTrials">The number of independent trials <c>N</c> (must be ≥ 2).</param>
    /// <param name="varianceOfTrialSharpes">
    /// The variance <c>V[{ŜR_n}]</c> across the trial Sharpe estimates, in the units in which the
    /// returned <c>ŜR₀</c> is desired (per-period if per-period variance is supplied).
    /// </param>
    /// <returns>The expected-maximum Sharpe ratio <c>ŜR₀</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="numTrials"/> &lt; 2 or <paramref name="varianceOfTrialSharpes"/> is negative.
    /// </exception>
    public static decimal ExpectedMaxSharpeRatio(int numTrials, decimal varianceOfTrialSharpes)
    {
        if (numTrials < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numTrials), numTrials, "At least two trials are required for the expected-maximum Sharpe ratio.");
        }

        if (varianceOfTrialSharpes < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(varianceOfTrialSharpes), varianceOfTrialSharpes, "Variance of trial Sharpe ratios must be non-negative.");
        }

        var n = numTrials;
        var maxZ = (1d - EulerMascheroni) * InverseNormal<double>.Evaluate(1d - 1d / n)
            + EulerMascheroni * InverseNormal<double>.Evaluate(1d - 1d / (n * Math.E));

        return (decimal)(Math.Sqrt((double)varianceOfTrialSharpes) * maxZ);
    }

    /// <summary>
    /// Evaluates the Deflated Sharpe Ratio (Bailey &amp; López de Prado 2014).
    /// </summary>
    /// <param name="observedSharpe">
    /// The observed Sharpe ratio, expressed at the <paramref name="tradingDaysPerYear"/> annualization.
    /// </param>
    /// <param name="numTrials">The number of independent trials <c>N</c> tried (must be ≥ 2).</param>
    /// <param name="varianceOfTrialSharpes">
    /// The variance <c>V[{ŜR_n}]</c> across the trial Sharpe estimates, at the same annualization
    /// as <paramref name="observedSharpe"/>.
    /// </param>
    /// <param name="numObservations">The number of observed returns <c>T</c> (must be ≥ 2).</param>
    /// <param name="skewness">The skewness <c>γ̂₃</c> of the return distribution.</param>
    /// <param name="kurtosis">The <b>full</b> (non-excess) kurtosis <c>γ̂₄</c> (Gaussian = 3).</param>
    /// <param name="tradingDaysPerYear">
    /// The annualization factor for <paramref name="observedSharpe"/> and
    /// <paramref name="varianceOfTrialSharpes"/> (must be ≥ 1; default 1 = per-period inputs).
    /// </param>
    /// <returns>The DSR, a probability strictly in <c>(0, 1)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="tradingDaysPerYear"/> &lt; 1 (other guards are delegated to
    /// <see cref="ExpectedMaxSharpeRatio"/> and <see cref="ProbabilisticSharpeRatio.Evaluate"/>).
    /// </exception>
    public static decimal Evaluate(
        decimal observedSharpe,
        int numTrials,
        decimal varianceOfTrialSharpes,
        int numObservations,
        decimal skewness,
        decimal kurtosis,
        int tradingDaysPerYear = 1)
    {
        if (tradingDaysPerYear < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tradingDaysPerYear), tradingDaysPerYear, "Trading days per year must be at least 1.");
        }

        // De-annualize: the PSR / ŜR₀ formulas are defined on per-period Sharpe units.
        var annualizationFactor = (decimal)Math.Sqrt(tradingDaysPerYear);
        var perPeriodSharpe = observedSharpe / annualizationFactor;
        var perPeriodVariance = varianceOfTrialSharpes / tradingDaysPerYear;

        var benchmark = ExpectedMaxSharpeRatio(numTrials, perPeriodVariance);

        return ProbabilisticSharpeRatio.Evaluate(
            perPeriodSharpe, benchmark, numObservations, skewness, kurtosis);
    }

    /// <summary>
    /// Evaluates the Deflated Sharpe Ratio, deriving <c>T</c>, skewness and kurtosis directly from a
    /// return series. Converts the excess kurtosis returned by
    /// <see cref="DecimalArrayExtensions.Kurtosis"/> to the <b>full</b> kurtosis the formula requires
    /// (<c>+3</c>) before the denominator.
    /// </summary>
    /// <param name="returns">The return series the Sharpe ratio was measured over (≥ 4 elements).</param>
    /// <param name="observedSharpe">
    /// The observed Sharpe ratio, expressed at the <paramref name="tradingDaysPerYear"/> annualization.
    /// </param>
    /// <param name="numTrials">The number of independent trials <c>N</c> tried (must be ≥ 2).</param>
    /// <param name="varianceOfTrialSharpes">
    /// The variance <c>V[{ŜR_n}]</c> across the trial Sharpe estimates, at the same annualization
    /// as <paramref name="observedSharpe"/>.
    /// </param>
    /// <param name="tradingDaysPerYear">The annualization factor (must be ≥ 1; default 1).</param>
    /// <returns>The DSR, a probability strictly in <c>(0, 1)</c>.</returns>
    /// <exception cref="EmptyOrNullArrayException">Thrown when <paramref name="returns"/> is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when <paramref name="returns"/> has fewer than four elements.</exception>
    public static decimal FromReturns(
        decimal[] returns,
        decimal observedSharpe,
        int numTrials,
        decimal varianceOfTrialSharpes,
        int tradingDaysPerYear = 1)
    {
        Guard.AgainstEmptyOrNullArray(() => returns);

        var skewness = returns.Skewness();
        var fullKurtosis = returns.Kurtosis() + 3m; // excess (Fisher) → full kurtosis (Gaussian = 3).

        return Evaluate(
            observedSharpe, numTrials, varianceOfTrialSharpes, returns.Length, skewness, fullKurtosis, tradingDaysPerYear);
    }
}
