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
using Boutquin.Trading.Domain.Exceptions;

/// <summary>
/// Computes the <b>Probabilistic Sharpe Ratio (PSR)</b> — the probability that a
/// strategy's <i>true</i> (population) Sharpe ratio exceeds a benchmark Sharpe ratio,
/// given the observed track record and its higher moments.
/// </summary>
/// <remarks>
/// <para>
/// Reference: Bailey, D. H. &amp; López de Prado, M. (2014), <i>"The Deflated Sharpe Ratio:
/// Correcting for Selection Bias, Backtest Overfitting, and Non-Normality"</i>,
/// Journal of Portfolio Management 40(5):94–107. This method implements the paper's
/// Eq. (2), p. 8:
/// </para>
/// <code>
/// PSR(SR*) = Z[ (ŜR − SR*)·√(T−1) / √(1 − γ̂₃·ŜR + ((γ̂₄−1)/4)·ŜR²) ]
/// </code>
/// <para>
/// where <c>Z[·]</c> is the standard-normal CDF, <c>ŜR</c> is the observed Sharpe ratio,
/// <c>T</c> is the number of observed returns, <c>γ̂₃</c> is the skewness and
/// <c>γ̂₄</c> is the <b>full (non-excess) kurtosis</b> of the return distribution.
/// </para>
/// <para>
/// <b>Kurtosis convention (normative).</b> The <c>kurtosis</c> parameter is the
/// <b>full</b> kurtosis, for which a Gaussian distribution has <c>γ̂₄ = 3</c> (confirmed by
/// the paper's worked example, p. 10). This is <i>not</i> the excess (Fisher) kurtosis that
/// <see cref="Domain.Extensions.DecimalArrayExtensions.Kurtosis"/> returns
/// (which is <c>γ̂₄ − 3</c>). Callers deriving the moment from a return array must convert
/// <c>fullKurtosis = returns.Kurtosis() + 3m</c> before calling; feeding the excess value
/// directly understates the denominator by <c>¾·ŜR²</c> and silently <i>inflates</i> the PSR.
/// </para>
/// <para>
/// The result lies strictly in the open interval <c>(0, 1)</c> (the standard-normal CDF
/// codomain), and equals exactly <c>0.5</c> when the benchmark equals the observed Sharpe
/// ratio (the numerator vanishes). Deterministic closed form — no convergence claim.
/// </para>
/// </remarks>
public static class ProbabilisticSharpeRatio
{
    /// <summary>
    /// Evaluates the Probabilistic Sharpe Ratio (Bailey &amp; López de Prado 2014, Eq. 2).
    /// </summary>
    /// <param name="observedSharpe">
    /// The observed Sharpe ratio <c>ŜR</c>, expressed at the same frequency as the returns
    /// underlying <paramref name="numObservations"/> (i.e. <b>non-annualized</b>).
    /// </param>
    /// <param name="benchmarkSharpe">
    /// The benchmark Sharpe ratio <c>SR*</c> being tested against, in the same units as
    /// <paramref name="observedSharpe"/>.
    /// </param>
    /// <param name="numObservations">The number of observed returns <c>T</c> (must be ≥ 2).</param>
    /// <param name="skewness">The skewness <c>γ̂₃</c> of the return distribution.</param>
    /// <param name="kurtosis">
    /// The <b>full</b> (non-excess) kurtosis <c>γ̂₄</c> of the return distribution
    /// (Gaussian = 3). See the type remarks for the excess→full conversion contract.
    /// </param>
    /// <returns>The PSR, a probability strictly in <c>(0, 1)</c>.</returns>
    /// <exception cref="InsufficientDataException">Thrown when <paramref name="numObservations"/> &lt; 2.</exception>
    /// <exception cref="CalculationException">
    /// Thrown when the variance-adjustment radicand <c>1 − γ̂₃·ŜR + ((γ̂₄−1)/4)·ŜR²</c> is
    /// non-positive, which would make the Sharpe-ratio standard error undefined.
    /// </exception>
    public static decimal Evaluate(
        decimal observedSharpe,
        decimal benchmarkSharpe,
        int numObservations,
        decimal skewness,
        decimal kurtosis)
    {
        if (numObservations < 2)
        {
            throw new InsufficientDataException(
                "At least two observations are required to compute the Probabilistic Sharpe Ratio.");
        }

        // Denominator: the (squared) standard error of the Sharpe estimator under non-normality.
        var radicand = 1m - skewness * observedSharpe
            + (kurtosis - 1m) / 4m * observedSharpe * observedSharpe;

        if (radicand <= 0m)
        {
            throw new CalculationException(
                "The Sharpe-ratio variance adjustment 1 − γ̂₃·ŜR + ((γ̂₄−1)/4)·ŜR² is non-positive; "
                + "PSR is undefined for these higher moments.");
        }

        var numerator = (double)(observedSharpe - benchmarkSharpe)
            * Math.Sqrt(numObservations - 1);
        var denominator = Math.Sqrt((double)radicand);
        var z = numerator / denominator;

        return (decimal)CumulativeNormal<double>.Evaluate(z);
    }
}
