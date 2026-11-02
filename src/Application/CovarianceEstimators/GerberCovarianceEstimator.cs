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

using Boutquin.Trading.Domain.Exceptions;

using NumericsStats = Boutquin.Numerics.Statistics;

namespace Boutquin.Trading.Application.CovarianceEstimators;

/// <summary>
/// Robust covariance estimator based on the <b>Gerber Statistic 2</b> — a co-movement
/// measure that counts only joint moves exceeding a per-asset threshold, discarding the
/// small (within-threshold) fluctuations a sample covariance treats as signal.
/// </summary>
/// <remarks>
/// <para>
/// For each asset <c>k</c> a threshold <c>H_k = c · σ_k</c> is formed from its sample
/// standard deviation (N-1 divisor, per the repository convention). An observation is
/// counted <i>up</i> when <c>r_tk ≥ +H_k</c> and <i>down</i> when <c>r_tk ≤ −H_k</c>;
/// moves with magnitude below <c>H_k</c> are ignored. Building the indicator matrices
/// <c>U</c> and <c>D</c> over the <c>T × N</c> panel, the concordant/discordant counts are
/// <c>N^UU = UᵀU</c>, <c>N^DD = DᵀD</c>, <c>N^UD = UᵀD</c>, <c>N^DU = DᵀU</c>.
/// </para>
/// <para>
/// The shipped <b>Gerber Statistic 2</b> (the variant proven positive semi-definite) is
/// <c>g²_ij = (N^UU_ij + N^DD_ij − N^UD_ij − N^DU_ij) / (T − N^NN_ij)</c>, where
/// <c>N^NN_ij = ⟨Pᵢ, Pⱼ⟩</c> with <c>P_tk = 1</c> iff asset <c>k</c> stayed <i>within</i> its
/// threshold at <c>t</c> (neither up nor down). Thus <c>N^NN_ij</c> is the <b>both-quiet</b>
/// count — observations in which <i>both</i> assets stayed within their thresholds — and the
/// denominator <c>T − N^NN_ij</c> equals the number of observations in which <b>at least one</b>
/// asset moved beyond its threshold (the <i>union</i> of large moves). This both-quiet
/// denominator is what Prop. 2 certifies PSD (via <c>X = PᵀP / T</c>); it is <i>not</i> the
/// both-large count <c>N^UU + N^DD + N^UD + N^DU</c> (Gerber Statistic 1's intersection family,
/// which is indefinite at finite T). The statistic is rescaled into a covariance via
/// <c>Σ_ij = g²_ij · σ_i · σ_j</c>. Because <c>g²_ii = 1</c>, the diagonal reproduces each
/// asset's sample variance: <c>Σ_ii = σ_i²</c>.
/// </para>
/// <para><b>Accuracy contract:</b> the Gerber Statistic 2 estimate is positive
/// semi-definite (Prop. 2 of the reference — Schur-product power-series proof), so the
/// rescaled covariance is PSD as well. The estimator is robust: it ignores within-threshold
/// noise and counts only large co-movements.</para>
/// <para><b>Intended omissions</b> (not built): an adaptive / cross-validated threshold
/// multiplier (<c>c</c> is fixed by the constructor, default <c>0.5</c>); the Gerber
/// Statistic 1 variant; and asymmetric thresholds (<c>H⁺ ≠ H⁻</c>).</para>
/// <para>
/// Reference: Gerber, Markowitz, Ernst, Miao, Javid &amp; Sargen (2023), "Proofs that the
/// Gerber Statistic is Positive Semidefinite", arXiv 2305.05663 (companion to "The Gerber
/// Statistic: A Robust Co-Movement Measure for Portfolio Optimization", J. Portfolio Mgmt
/// 48(3), 2022).
/// </para>
/// </remarks>
public sealed class GerberCovarianceEstimator : ICovarianceEstimator
{
    private static readonly NumericsStats.SampleCovarianceEstimator s_sampleCov = new();

    private readonly decimal _c;

    /// <summary>
    /// Initializes a new instance of the <see cref="GerberCovarianceEstimator"/> class.
    /// </summary>
    /// <param name="c">
    /// The threshold multiplier applied to each asset's standard deviation
    /// (<c>H_k = c · σ_k</c>). Must be strictly positive. The canonical value is
    /// <c>0.5</c> (the constructor default).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="c"/> is not strictly positive.</exception>
    public GerberCovarianceEstimator(decimal c = 0.5m)
    {
        if (c <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(c), c, "Threshold multiplier c must be strictly positive.");
        }

        _c = c;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when the return series have different lengths or fewer than two observations.</exception>
    /// <exception cref="CalculationException">Thrown when any asset has zero sample standard deviation (a degenerate, constant series).</exception>
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);

        var n = returns.Length;
        var t = returns[0].Length;

        // Per-asset sample standard deviation (N-1 divisor). The sample covariance's
        // diagonal holds the variances; sigma_k = sqrt(var_k).
        var sampleCov = s_sampleCov.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
        var sigma = new decimal[n];
        for (var k = 0; k < n; k++)
        {
            var variance = sampleCov[k, k];
            if (variance <= 0m)
            {
                throw new CalculationException(
                    $"Asset at index {k} has zero sample variance (constant return series); " +
                    "the Gerber statistic is undefined for a degenerate asset.");
            }

            sigma[k] = (decimal)Math.Sqrt((double)variance);
        }

        // Indicator matrices U (up) and D (down), each T×N. u[t,k]=1 if r_tk >= +H_k,
        // d[t,k]=1 if r_tk <= -H_k, where H_k = c * sigma_k.
        var u = new bool[t, n];
        var d = new bool[t, n];
        for (var k = 0; k < n; k++)
        {
            var threshold = _c * sigma[k];
            var series = returns[k];
            for (var obs = 0; obs < t; obs++)
            {
                var r = series[obs];
                if (r >= threshold)
                {
                    u[obs, k] = true;
                }
                else if (r <= -threshold)
                {
                    d[obs, k] = true;
                }
            }
        }

        // Gerber Statistic 2 (the PSD-proven variant, Prop. 2 of the reference):
        //   g2_ij = (N^UU + N^DD - N^UD - N^DU)_ij / (T - N^NN_ij)
        // where N^NN_ij = <P_i, P_j> is the BOTH-QUIET count — the number of observations
        // in which BOTH asset i and asset j stayed within their thresholds (P_tk = 1 iff
        // |r_tk| < H_k = neither up nor down). The proof's denominator is therefore
        //   T - N^NN_ij = #{observations where AT LEAST ONE of i, j is large} (the UNION of
        // large moves), NOT the both-large count N^UU+N^DD+N^UD+N^DU (which is Gerber
        // Statistic 1's intersection family and is materially indefinite at finite T).
        // Rescale: Sigma_ij = g2_ij * sigma_i * sigma_j.
        var cov = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                var nUu = 0;
                var nDd = 0;
                var nUd = 0;
                var nDu = 0;
                var nNeitherLarge = 0; // N^NN_ij: both i and j within threshold (both-quiet)
                for (var obs = 0; obs < t; obs++)
                {
                    var ui = u[obs, i];
                    var di = d[obs, i];
                    var uj = u[obs, j];
                    var dj = d[obs, j];
                    var iLarge = ui || di;
                    var jLarge = uj || dj;

                    if (ui && uj)
                    {
                        nUu++;
                    }
                    else if (di && dj)
                    {
                        nDd++;
                    }
                    else if (ui && dj)
                    {
                        nUd++;
                    }
                    else if (di && uj)
                    {
                        nDu++;
                    }

                    if (!iLarge && !jLarge)
                    {
                        nNeitherLarge++;
                    }
                }

                var concordant = nUu + nDd;
                var discordant = nUd + nDu;
                // PSD-proven Gerber Statistic 2 denominator: T - N^NN_ij = count of
                // observations where at least one of i, j is large (the union).
                var atLeastOneLarge = t - nNeitherLarge;

                decimal g2;
                if (i == j)
                {
                    // The diagonal of Gerber Statistic 2 is 1 by definition: an asset is
                    // perfectly concordant with itself (N^UD_ii = N^DU_ii = 0, since an
                    // observation cannot be both up and down), so g2_ii = 1 whenever the
                    // statistic is defined. This must NOT depend on the denominator: when c
                    // is large enough that every observation falls within +/- c*sigma_i,
                    // atLeastOneLarge_ii = 0, yet the diagonal must still rescale to
                    // sigma_i^2 (the documented Sigma_ii = sigma_i^2 accuracy contract).
                    // Forcing g2_ii = 1 here keeps that contract for all valid c.
                    g2 = 1m;
                }
                else if (atLeastOneLarge == 0)
                {
                    // No observation had either asset large for this off-diagonal pair: the
                    // pair contributes no co-movement signal, so g2_ij = 0 is the specified
                    // sentinel (genuinely no measured co-movement).
                    g2 = 0m;
                }
                else
                {
                    g2 = (decimal)(concordant - discordant) / atLeastOneLarge;
                }

                var value = g2 * sigma[i] * sigma[j];
                cov[i, j] = value;
                cov[j, i] = value;
            }
        }

        return cov;
    }
}
