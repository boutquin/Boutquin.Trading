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
/// <b>DCC-NL</b> — the Dynamic Conditional Correlation model with a <b>nonlinearly-shrunk
/// correlation-targeting intercept</b> (Engle, Ledoit &amp; Wolf, "Large Dynamic Covariance
/// Matrices", JBES 2019). Produces a time-varying conditional covariance matrix
/// <c>H_t = D_t R_t D_t</c>: per-asset GARCH(1,1) conditional volatilities on the diagonal
/// of <c>D_t</c>, and a DCC-evolved conditional correlation <c>R_t</c> whose unconditional
/// target is the NL-shrunk correlation matrix rather than the noisy sample correlation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step 1 — per-asset GARCH(1,1).</b> Each asset's return series is fitted with
/// <see cref="NumericsStats.GarchOneOneEstimator{T}"/>, yielding the conditional variances
/// <c>d²_{i,t} = ω_i + a_i r²_{i,t−1} + b_i d²_{i,t−1}</c> and the devolatilized
/// standardized residuals <c>s_{i,t} = r_{i,t} / d_{i,t}</c>.
/// </para>
/// <para>
/// <b>Step 2 — DCC correlation targeting.</b> The auxiliary matrix evolves as
/// <c>Q_t = (1−α−β) C̃ + α s_{t−1} s'_{t−1} + β Q_{t−1}</c>, normalized to a correlation
/// matrix <c>R_t = diag(Q_t)^{−1/2} Q_t diag(Q_t)^{−1/2}</c>, and combined with the GARCH
/// vols into <c>H_t = D_t R_t D_t</c>, where <c>D_t = diag(d_{1,t}, …, d_{N,t})</c>.
/// </para>
/// <para>
/// <b>Step 3 — NL-shrunk intercept C̃ (the key innovation).</b> The unconditional target
/// <c>C̃</c> is a <i>nonlinearly-shrunk</i> correlation matrix, not the sample correlation
/// of the standardized residuals. In this V1 the NL shrinkage is approximated by the
/// repository's existing Marcenko-Pastur denoiser
/// (<see cref="DenoisedCovarianceEstimator"/>, Jacobi eigen-kernel) applied to the
/// unit-variance standardized residuals, then renormalized to a correlation matrix. This is
/// the documented Jacobi fallback the spec sanctions for the full QuEST Stieltjes-transform
/// inversion.
/// </para>
/// <para>
/// <b>Accuracy contract.</b> When the DCC stationarity condition <c>α+β&lt;1</c> holds, every
/// per-date <c>H_t</c> (and the one-step forecast) is positive semi-definite: <c>Q_t</c> is a
/// non-negative combination of the PSD intercept <c>C̃</c>, the rank-1 PSD outer product
/// <c>s_{t−1}s'_{t−1}</c>, and the PSD <c>Q_{t−1}</c>; correlation normalization preserves PSD;
/// and conjugation by the non-negative diagonal <c>D_t</c> preserves PSD. The constructor
/// enforces <c>α+β&lt;1</c>.
/// </para>
/// <para>
/// <b>Concurrency contract (V1).</b> Single-threaded by contract: <see cref="Fit"/> is a
/// single-writer that must run to completion before any reader; it is not thread-safe during
/// execution and not reentrant. After a completed <see cref="Fit"/> the fitted state is
/// immutable until the next <see cref="Fit"/>, so concurrent reads are safe.
/// </para>
/// <para>
/// <b>Parameter estimation (V2).</b> The DCC scalars <c>(α, β)</c> are estimated from the return
/// window by <b>full two-step quasi-maximum-likelihood</b> (Engle 2002, eq. 10) by default; the
/// constructor <c>alpha</c>/<c>beta</c> serve as the optimizer's initial guess. Pass
/// <c>estimateParameters: false</c> to hold them fixed at the supplied values (the V1 behavior).
/// The full QMLE is used in place of the reference's composite-likelihood (2-MSCLE) estimator
/// because the target universe is small (<c>N ≤ 10</c>), where the full N×N likelihood is tractable
/// (<c>O(T·N³)</c> per evaluation) and more efficient; the composite-likelihood path for the
/// large-N regime (<c>N ≫ T</c>) remains a separate-spec concern.
/// </para>
/// <para>
/// <b>Intended omissions.</b> The exact QuEST
/// Stieltjes-transform eigenvalue inversion (Jacobi denoiser used instead); the composite-likelihood
/// (2-MSCLE) estimator for the large-N regime; cDCC (Aielli 2013); asymmetric GARCH (GJR / EGARCH);
/// BEKK; and multi-step forecasting (only one-step is provided).
/// </para>
/// <para>
/// Reference: Engle, Ledoit &amp; Wolf (2019), "Large Dynamic Covariance Matrices",
/// <i>Journal of Business &amp; Economic Statistics</i> 37(2), 363–375.
/// </para>
/// </remarks>
public sealed class DccNlCovarianceEstimator : IDynamicCovarianceEstimator
{
    private const int MinAssets = 2;
    private const int MinObservations = 10;
    private const double VarianceFloor = 1e-12;

    private readonly double _alpha;
    private readonly double _beta;
    private readonly bool _estimateParameters;
    private readonly ILogger<DccNlCovarianceEstimator> _logger;

    // Fitted state (null until a successful Fit).
    private IReadOnlyList<decimal[,]>? _conditionalCovariances;
    private decimal[,]? _forecast;

    /// <summary>
    /// Initializes a new instance of the <see cref="DccNlCovarianceEstimator"/> class.
    /// </summary>
    /// <param name="alpha">
    /// The DCC innovation weight <c>α</c> on the most recent standardized-residual outer product.
    /// When <paramref name="estimateParameters"/> is <see langword="true"/> (the default) this is
    /// the optimizer's <i>initial guess</i>; when <see langword="false"/> it is the fixed value
    /// used in the recursion. Must be in <c>(0, 1)</c>. Default <c>0.01</c>.
    /// </param>
    /// <param name="beta">
    /// The DCC persistence weight <c>β</c> on the previous auxiliary matrix. When
    /// <paramref name="estimateParameters"/> is <see langword="true"/> (the default) this is the
    /// optimizer's <i>initial guess</i>; when <see langword="false"/> it is the fixed value used in
    /// the recursion. Must be in <c>(0, 1)</c>. Default <c>0.97</c>.
    /// </param>
    /// <param name="estimateParameters">
    /// When <see langword="true"/> (the default), <see cref="Fit"/> estimates <c>(α, β)</c> from the
    /// return window by two-step quasi-maximum-likelihood (Engle 2002, eq. 10), using
    /// <paramref name="alpha"/>/<paramref name="beta"/> as the initial guess. When
    /// <see langword="false"/>, <c>(α, β)</c> are held fixed at the supplied values — the V1
    /// fixed-parameter behavior.
    /// </param>
    /// <param name="logger">
    /// Optional logger for estimation-convergence observability. When the QMLE optimizer cannot
    /// converge (or wanders to a degenerate argmin) and <see cref="Fit"/> silently falls back to the
    /// initial <c>(α, β)</c>, a warning is emitted here so a backtest is not handed un-estimated
    /// parameters while believing it received an estimated fit, and so a window that silently mixes
    /// estimated and default parameterizations is diagnosable rather than mistaken for a regime shift.
    /// Defaults to <see cref="NullLogger{T}.Instance"/> (no-op) when omitted.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="alpha"/> or <paramref name="beta"/> is outside <c>(0, 1)</c>,
    /// or when the stationarity condition <c>α+β&lt;1</c> is violated (<c>α+β≥1</c>).
    /// </exception>
    public DccNlCovarianceEstimator(
        decimal alpha = 0.01m,
        decimal beta = 0.97m,
        bool estimateParameters = true,
        ILogger<DccNlCovarianceEstimator>? logger = null)
    {
        if (alpha <= 0m || alpha >= 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alpha), alpha, "DCC alpha must lie strictly in (0, 1).");
        }

        if (beta <= 0m || beta >= 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(beta), beta, "DCC beta must lie strictly in (0, 1).");
        }

        if (alpha + beta >= 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(beta),
                beta,
                $"DCC stationarity requires alpha + beta < 1, but alpha + beta = {alpha + beta}.");
        }

        _alpha = (double)alpha;
        _beta = (double)beta;
        _estimateParameters = estimateParameters;
        _logger = logger ?? NullLogger<DccNlCovarianceEstimator>.Instance;
    }

    /// <inheritdoc />
    public void Fit(decimal[][] returns)
    {
        var (s, d2, oneStepVar, cTilde, n, t) = PreprocessReturns(returns, _logger);

        // Estimate the DCC scalars by two-step QMLE, or use the constructor values when opted out.
        double dccAlpha, dccBeta;
        if (_estimateParameters)
        {
            (dccAlpha, dccBeta) = EstimateDccParameters(_alpha, _beta, cTilde, s, n, t, _logger);
        }
        else
        {
            (dccAlpha, dccBeta) = (_alpha, _beta);
        }

        // ── Step 2: DCC recursion Q_t → R_t → H_t, plus one-step forecast ──
        var (covariances, forecast) =
            RunDccRecursion(dccAlpha, dccBeta, cTilde, s, d2, oneStepVar, n, t);

        // Publish fitted state last (single-writer; readers see a fully-built result).
        _conditionalCovariances = covariances;
        _forecast = forecast;
    }

    /// <summary>
    /// Validates the return panel and runs the GARCH devolatilization (Step 1) and the NL-shrunk
    /// correlation-targeting intercept (Step 3), producing the inputs the DCC recursion and the
    /// QMLE objective consume. Extracted from <see cref="Fit"/> so the opt-out equivalence tests can
    /// drive <see cref="RunDccRecursion"/> with the exact <c>(C̃, s)</c> the production path computes.
    /// </summary>
    /// <param name="returns">The per-asset return series, jagged <c>returns[asset][time]</c>.</param>
    /// <param name="logger">
    /// Optional logger. A per-asset GARCH(1,1) non-convergence is logged at warning level (the softer,
    /// structured complement to the <see cref="CalculationException"/> it then throws) so the failing
    /// asset index is recorded even if a caller later catches the exception. Defaults to no-op.
    /// </param>
    /// <returns>
    /// The standardized residual panel <c>s[time][asset]</c>; the per-asset GARCH conditional
    /// variances <c>d2[asset][time]</c>; the per-asset one-step variance forecasts; the NL-shrunk
    /// correlation intercept C̃; and the dimensions <c>(n, t)</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="returns"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there are fewer than <see cref="MinAssets"/> assets or fewer than
    /// <see cref="MinObservations"/> observations, or when a series is null or length-mismatched.
    /// </exception>
    /// <exception cref="CalculationException">
    /// Thrown when an asset has near-zero variance (devolatilization <c>r/d</c> is undefined).
    /// </exception>
    internal static (double[][] s, double[][] d2, double[] oneStepVar, double[,] cTilde, int n, int t)
        PreprocessReturns(decimal[][] returns, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var n = returns.Length;
        if (n < MinAssets)
        {
            throw new ArgumentException(
                $"DCC-NL requires at least {MinAssets} assets, but received {n}.",
                nameof(returns));
        }

        var t = returns[0].Length;
        if (t < MinObservations)
        {
            throw new ArgumentException(
                $"DCC-NL requires at least {MinObservations} observations, but received {t}.",
                nameof(returns));
        }

        for (var i = 0; i < n; i++)
        {
            if (returns[i] is null)
            {
                throw new ArgumentException(
                    $"Asset return series at index {i} is null.", nameof(returns));
            }

            if (returns[i].Length != t)
            {
                throw new ArgumentException(
                    "All asset return series must have the same length.", nameof(returns));
            }
        }

        // ── Step 1: per-asset GARCH(1,1) ───────────────────────────────────
        // d2[i][k]  = conditional variance d²_{i,k}
        // s[k][i]   = standardized residual s_{i,k} = r_{i,k} / d_{i,k}
        var d2 = new double[n][];
        var oneStepVar = new double[n];
        var s = new double[t][];
        for (var k = 0; k < t; k++)
        {
            s[k] = new double[n];
        }

        for (var i = 0; i < n; i++)
        {
            var series = new double[t];
            for (var k = 0; k < t; k++)
            {
                series[k] = (double)returns[i][k];
            }

            // Near-zero variance assets are degenerate — the GARCH likelihood and the
            // devolatilization r/d are undefined.
            var mean = series.Average();
            var ss = 0.0;
            for (var k = 0; k < t; k++)
            {
                var dev = series[k] - mean;
                ss += dev * dev;
            }

            var sampleVariance = ss / (t - 1);
            if (sampleVariance < VarianceFloor)
            {
                throw new CalculationException(
                    $"Asset at index {i} has near-zero variance ({sampleVariance:E3}); " +
                    "DCC-NL devolatilization r/d is undefined for a (near-)constant series.");
            }

            var fit = NumericsStats.GarchOneOneEstimator<double>.Fit(series);

            // Surface a non-converged GARCH fit rather than silently devolatilizing with its
            // unreliable conditional variances / standardized residuals (degenerate inputs are
            // surfaced, not propagated — see the Financial Calculation Conventions).
            if (!fit.Converged)
            {
                logger?.LogWarning(
                    "DCC-NL: GARCH(1,1) fit for the asset at index {AssetIndex} did not converge; its "
                    + "conditional variances and standardized residuals are unreliable for "
                    + "devolatilization. Surfacing as a CalculationException.",
                    i);

                throw new CalculationException(
                    $"GARCH(1,1) fit for the asset at index {i} did not converge; its conditional "
                    + "variances and standardized residuals are unreliable for DCC-NL devolatilization.");
            }

            d2[i] = fit.ConditionalVariances;

            // Use the GARCH model's own demeaned standardized residuals ε_t/√h_t, NOT the raw
            // return divided by the innovation vol. GarchOneOneEstimator fits an AR(1) mean
            // (r_t = μ + φ·r_{t−1} + ε_t); dividing the raw r_t by √h_t would leave the conditional
            // mean in s, contaminating C̃, the QMLE objective, the estimated (α, β), and every H_t.
            var standardizedResiduals = fit.StandardizedResiduals;
            for (var k = 0; k < t; k++)
            {
                s[k][i] = standardizedResiduals[k];
            }

            oneStepVar[i] = Math.Max(fit.ForecastVariance(1), VarianceFloor);
        }

        // ── Step 3: NL-shrunk intercept C̃ ─────────────────────────────────
        var cTilde = ComputeNlShrunkCorrelation(s, n, t);

        return (s, d2, oneStepVar, cTilde, n, t);
    }

    /// <inheritdoc />
    public IReadOnlyList<decimal[,]> GetConditionalCovariances()
    {
        return _conditionalCovariances
               ?? throw new InvalidOperationException(
                   "Fit must be called before GetConditionalCovariances.");
    }

    /// <inheritdoc />
    public decimal[,] Forecast()
    {
        return _forecast
               ?? throw new InvalidOperationException("Fit must be called before Forecast.");
    }

    /// <summary>
    /// Advances the DCC auxiliary matrix one step:
    /// <c>Q_k = (1−α−β) C̃ + α s_{k−1} s'_{k−1} + β Q_{k−1}</c>. Shared by the covariance recursion
    /// (<see cref="RunDccRecursion"/>), the one-step forecast, and the QMLE objective
    /// (<see cref="NegativeQcl"/>) so the three agree on exactly one model definition.
    /// </summary>
    private static double[,] AdvanceQ(
        double[,] q,
        double[] prevS,
        double[,] cTilde,
        double omega,
        double alpha,
        double beta,
        int n)
    {
        var next = new double[n, n];
        for (var a = 0; a < n; a++)
        {
            for (var b = 0; b < n; b++)
            {
                next[a, b] = (omega * cTilde[a, b])
                             + (alpha * prevS[a] * prevS[b])
                             + (beta * q[a, b]);
            }
        }

        return next;
    }

    /// <summary>
    /// Runs the DCC correlation-targeting recursion
    /// <c>Q_t = (1−α−β) C̃ + α s_{t−1} s'_{t−1} + β Q_{t−1}</c> (with <c>Q_0 = C̃</c>) for the
    /// supplied scalar parameters, normalizes each <c>Q_t</c> to the conditional correlation
    /// <c>R_t = diag(Q_t)^{−1/2} Q_t diag(Q_t)^{−1/2}</c>, and rescales by the per-date GARCH
    /// volatilities to the conditional covariance <c>H_t = D_t R_t D_t</c>. Also produces the
    /// one-step-ahead forecast <c>H_{T+1}</c> using the one-step GARCH variance forecasts on the
    /// diagonal.
    /// </summary>
    /// <param name="alpha">The DCC innovation weight <c>α</c>.</param>
    /// <param name="beta">The DCC persistence weight <c>β</c>.</param>
    /// <param name="cTilde">The NL-shrunk correlation intercept C̃ (the unconditional target).</param>
    /// <param name="s">The standardized residual panel, indexed <c>s[time][asset]</c>.</param>
    /// <param name="d2">The per-asset GARCH conditional variances, indexed <c>d2[asset][time]</c>.</param>
    /// <param name="oneStepVar">The per-asset one-step-ahead GARCH variance forecasts.</param>
    /// <param name="n">The number of assets.</param>
    /// <param name="t">The number of observations.</param>
    /// <returns>
    /// The full sequence of <c>t</c> conditional covariance matrices (each symmetrized) and the
    /// symmetrized one-step-ahead forecast covariance.
    /// </returns>
    /// <remarks>
    /// Pure and stateless: reads only its arguments and allocates only local results, holding no
    /// instance or static mutable state. Safe to call repeatedly with candidate <c>(α, β)</c> pairs.
    /// </remarks>
    internal static (List<decimal[,]> covariances, decimal[,] forecast) RunDccRecursion(
        double alpha,
        double beta,
        double[,] cTilde,
        double[][] s,
        double[][] d2,
        double[] oneStepVar,
        int n,
        int t)
    {
        var omega = 1.0 - alpha - beta; // > 0 because alpha + beta < 1.
        var q = (double[,])cTilde.Clone(); // Q_0 = C̃.
        var covariances = new List<decimal[,]>(t);

        for (var k = 0; k < t; k++)
        {
            if (k > 0)
            {
                q = AdvanceQ(q, s[k - 1], cTilde, omega, alpha, beta, n);
            }

            // R_k = diag(Q)^{-1/2} Q diag(Q)^{-1/2}; H_k = D_k R_k D_k.
            // D_k = diag of GARCH conditional volatilities at date k.
            var hk = new decimal[n, n];
            for (var a = 0; a < n; a++)
            {
                var qaa = Math.Sqrt(Math.Max(q[a, a], VarianceFloor));
                var da = Math.Sqrt(Math.Max(d2[a][k], VarianceFloor));
                for (var b = 0; b < n; b++)
                {
                    var qbb = Math.Sqrt(Math.Max(q[b, b], VarianceFloor));
                    var db = Math.Sqrt(Math.Max(d2[b][k], VarianceFloor));
                    var r = q[a, b] / (qaa * qbb);
                    hk[a, b] = (decimal)(da * db * r);
                }
            }

            covariances.Add(Symmetrize(hk, n));
        }

        // ── One-step-ahead forecast ────────────────────────────────────────
        // Q_{T+1} = omega*C̃ + alpha * s_{T-1} s_{T-1}' + beta * Q_T  (q holds Q_T after the loop).
        var qNext = AdvanceQ(q, s[t - 1], cTilde, omega, alpha, beta, n);

        var forecast = new decimal[n, n];
        for (var a = 0; a < n; a++)
        {
            var qaa = Math.Sqrt(Math.Max(qNext[a, a], VarianceFloor));
            var da = Math.Sqrt(oneStepVar[a]);
            for (var b = 0; b < n; b++)
            {
                var qbb = Math.Sqrt(Math.Max(qNext[b, b], VarianceFloor));
                var db = Math.Sqrt(oneStepVar[b]);
                var r = qNext[a, b] / (qaa * qbb);
                forecast[a, b] = (decimal)(da * db * r);
            }
        }

        return (covariances, Symmetrize(forecast, n));
    }

    /// <summary>
    /// The negative DCC quasi log-likelihood
    /// <c>nQCL(α,β) = ½ Σ_t [log|R_t| + s_t′ R_t⁻¹ s_t]</c> (Engle 2002, eq. 10) for the supplied
    /// scalar parameters, given the pre-computed correlation-targeting intercept C̃ and the
    /// standardized residual panel. Runs only the pure-correlation recursion <c>Q_t → R_t</c>
    /// (no GARCH <c>D_t</c> scaling — <c>R_t</c> is a correlation matrix).
    /// </summary>
    /// <param name="alpha">Candidate DCC innovation weight <c>α</c>.</param>
    /// <param name="beta">Candidate DCC persistence weight <c>β</c>.</param>
    /// <param name="cTilde">The NL-shrunk correlation intercept C̃ (the unconditional target).</param>
    /// <param name="s">The standardized residual panel, indexed <c>s[time][asset]</c>.</param>
    /// <param name="n">The number of assets.</param>
    /// <param name="t">The number of observations.</param>
    /// <returns>
    /// The negative quasi log-likelihood, or <see cref="double.MaxValue"/> when any
    /// <c>R_t</c> is near-singular (Cholesky fails) or the accumulated term is non-finite — so a
    /// Nelder-Mead simplex probing the <c>α+β→1</c> boundary rejects the candidate rather than
    /// propagating <c>NaN</c>/<c>±∞</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <c>log|R_t|</c> and <c>s_t′ R_t⁻¹ s_t</c> are computed from the Cholesky factor
    /// <c>L L′ = Q_t</c> without forming the inverse: with <c>z_i = √Q_{ii} · s_{t,i}</c>,
    /// <c>log|R_t| = 2 Σ_i log(L_{ii} / √Q_{ii})</c> and
    /// <c>s_t′ R_t⁻¹ s_t = z′ Q_t⁻¹ z = ‖L⁻¹ z‖²</c> via forward substitution.
    /// </para>
    /// <para>Pure and stateless — reads only its arguments and allocates only local results.</para>
    /// </remarks>
    internal static double NegativeQcl(
        double alpha,
        double beta,
        double[,] cTilde,
        double[][] s,
        int n,
        int t)
    {
        var omega = 1.0 - alpha - beta;
        var q = (double[,])cTilde.Clone(); // Q_0 = C̃.
        var total = 0.0;

        for (var k = 0; k < t; k++)
        {
            if (k > 0)
            {
                q = AdvanceQ(q, s[k - 1], cTilde, omega, alpha, beta, n);
            }

            // Cholesky L L' = Q_t. Q_t is PSD under the recursion; near-singular ⇒ reject.
            if (!TryCholesky(q, n, out var l))
            {
                return double.MaxValue;
            }

            // log|R_t| = 2 Σ log(L_ii / sqrt(Q_ii)).
            var logDet = 0.0;
            for (var i = 0; i < n; i++)
            {
                var qii = q[i, i];
                if (qii <= 0.0 || l[i, i] <= 0.0)
                {
                    return double.MaxValue;
                }

                logDet += 2.0 * Math.Log(l[i, i] / Math.Sqrt(qii));
            }

            // s_t' R_t⁻¹ s_t = z' Q_t⁻¹ z, z_i = sqrt(Q_ii) s_{t,i}; forward-solve L y = z, quad = y'y.
            var sk = s[k];
            var y = new double[n];
            for (var i = 0; i < n; i++)
            {
                var sum = Math.Sqrt(q[i, i]) * sk[i];
                for (var j = 0; j < i; j++)
                {
                    sum -= l[i, j] * y[j];
                }

                y[i] = sum / l[i, i];
            }

            var quad = 0.0;
            for (var i = 0; i < n; i++)
            {
                quad += y[i] * y[i];
            }

            var term = logDet + quad;
            if (!double.IsFinite(term))
            {
                return double.MaxValue;
            }

            total += term;
        }

        return 0.5 * total;
    }

    /// <summary>
    /// Estimates DCC scalar parameters by minimising the negative quasi log-likelihood
    /// <see cref="NegativeQcl"/> via <see cref="NelderMeadMinimizer{T}"/> in
    /// softmax-reparameterized <c>(u, v) ∈ ℝ²</c> space, where
    /// <c>α = eᵘ / (1+eᵘ+eᵛ)</c>, <c>β = eᵛ / (1+eᵘ+eᵛ)</c>. The mapping keeps the simplex away
    /// from the constraint boundaries and guarantees <c>α &gt; 0</c>, <c>β &gt; 0</c>,
    /// <c>α+β &lt; 1</c> for every finite <c>(u, v)</c>.
    /// </summary>
    /// <param name="alphaInit">Initial guess for <c>α</c> (the constructor value). Must satisfy <c>α+β&lt;1</c>.</param>
    /// <param name="betaInit">Initial guess for <c>β</c> (the constructor value). Must satisfy <c>α+β&lt;1</c>.</param>
    /// <param name="cTilde">The NL-shrunk correlation intercept C̃.</param>
    /// <param name="s">The standardized residual panel, indexed <c>s[time][asset]</c>.</param>
    /// <param name="n">The number of assets.</param>
    /// <param name="t">The number of observations.</param>
    /// <param name="logger">
    /// Optional logger. When the optimizer does not converge, or converges to a degenerate
    /// (non-stationary) argmin, and the estimate falls back to the initial guess, a warning is emitted
    /// here so the silent fallback is observable to the caller. Defaults to no-op when omitted.
    /// </param>
    /// <returns>
    /// Estimated <c>(alphaHat, betaHat)</c> satisfying <c>α &gt; 0</c>, <c>β &gt; 0</c>,
    /// <c>α+β &lt; 1</c>; or the initial-guess values when the optimizer does not converge.
    /// </returns>
    internal static (double alphaHat, double betaHat) EstimateDccParameters(
        double alphaInit,
        double betaInit,
        double[,] cTilde,
        double[][] s,
        int n,
        int t,
        ILogger? logger = null)
    {
        // The softmax inverse below requires a feasible initial guess: γ₀ = 1−α−β must be > 0,
        // else log(α/γ₀) is NaN/∞. Production always satisfies this (constructor pre-condition),
        // but reject an infeasible guess explicitly rather than coercing it into an infeasible
        // estimate that would break PSD downstream.
        if (alphaInit <= 0.0 || betaInit <= 0.0 || alphaInit + betaInit >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alphaInit),
                $"DCC QMLE requires a feasible initial guess (alpha > 0, beta > 0, alpha + beta < 1), " +
                $"but received alpha = {alphaInit}, beta = {betaInit}.");
        }

        var gamma0 = 1.0 - alphaInit - betaInit; // > 0 by the feasibility guard above.
        var u0 = Math.Log(alphaInit / gamma0);
        var v0 = Math.Log(betaInit / gamma0);
        var initialGuess = new[] { u0, v0 };

        static (double a, double b) FromUV(double u, double v)
        {
            var eu = Math.Exp(u);
            var ev = Math.Exp(v);
            var d = 1.0 + eu + ev;
            return (eu / d, ev / d);
        }

        double Objective(double[] uv)
        {
            var (a, b) = FromUV(uv[0], uv[1]);
            return NegativeQcl(a, b, cTilde, s, n, t);
        }

        var result = NelderMeadMinimizer<double>.Minimize(
            Objective,
            initialGuess,
            new SimplexOptions<double>(MaxIterations: 5000));

        if (!result.Converged)
        {
            // Fallback: initial guess. DCC recursion proceeds with constructor defaults.
            logger?.LogWarning(
                "DCC-NL QMLE did not converge within {Iterations} simplex iterations; falling back to "
                + "the initial DCC parameters (alpha={Alpha}, beta={Beta}). The conditional covariances "
                + "for this window use UN-ESTIMATED parameters — do not read a regime shift into a window "
                + "that mixes estimated and fallback fits.",
                result.Iterations,
                alphaInit,
                betaInit);

            return (alphaInit, betaInit);
        }

        var (alphaHat, betaHat) = FromUV(result.Argmin[0], result.Argmin[1]);

        // The softmax guarantees α+β<1 for all *finite* (u, v), but an optimizer that wanders to
        // an extreme argmin can overflow Math.Exp (eᵘ → ∞), collapsing FromUV to α+β = 1. Defend
        // the stationarity invariant by falling back to the initial guess in that degenerate case.
        if (!(alphaHat > 0.0 && betaHat > 0.0 && alphaHat + betaHat < 1.0))
        {
            logger?.LogWarning(
                "DCC-NL QMLE converged to a degenerate, non-stationary argmin "
                + "(alpha={AlphaHat}, beta={BetaHat}); falling back to the initial DCC parameters "
                + "(alpha={Alpha}, beta={Beta}). The conditional covariances for this window use "
                + "UN-ESTIMATED parameters.",
                alphaHat,
                betaHat,
                alphaInit,
                betaInit);

            return (alphaInit, betaInit);
        }

        return (alphaHat, betaHat);
    }

    /// <summary>
    /// Attempts the lower-triangular Cholesky factorization <c>L L′ = m</c> of a symmetric matrix.
    /// Returns <c>false</c> (rather than throwing) when <paramref name="m"/> is not
    /// positive-definite, so callers can treat near-singularity as a rejected candidate.
    /// </summary>
    /// <remarks>
    /// Deliberately a non-throwing variant rather than <c>Boutquin.Numerics.LinearAlgebra.
    /// CholeskyDecomposition&lt;double&gt;.Decompose</c>, which throws on a non-PD input. This runs
    /// inside the QMLE objective on every Nelder-Mead probe (up to 5000 × T evaluations); a
    /// candidate near the <c>α+β→1</c> boundary can yield a non-PD <c>Q_t</c>, and using exceptions
    /// for that expected control-flow on the optimizer hot path would be a performance anti-pattern.
    /// The <c>bool</c>-return form lets <see cref="NegativeQcl"/> reject such candidates branch-free.
    /// </remarks>
    private static bool TryCholesky(double[,] m, int n, out double[,] l)
    {
        l = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j <= i; j++)
            {
                var sum = m[i, j];
                for (var p = 0; p < j; p++)
                {
                    sum -= l[i, p] * l[j, p];
                }

                if (i == j)
                {
                    if (sum <= 0.0)
                    {
                        return false;
                    }

                    l[i, i] = Math.Sqrt(sum);
                }
                else
                {
                    l[i, j] = sum / l[j, j];
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Computes the NL-shrunk correlation intercept C̃ from the standardized residuals.
    /// Builds the sample covariance of the unit-scaled residuals, applies the
    /// Marcenko-Pastur denoiser (the V1 Jacobi fallback for QuEST), and renormalizes the
    /// result to a correlation matrix (unit diagonal).
    /// </summary>
    private static double[,] ComputeNlShrunkCorrelation(double[][] s, int n, int t)
    {
        // Re-shape s[time][asset] into the jagged returns[asset][time] decimal panel the
        // static denoiser consumes.
        var panel = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            panel[i] = new decimal[t];
            for (var k = 0; k < t; k++)
            {
                panel[i][k] = (decimal)s[k][i];
            }
        }

        var denoised = new DenoisedCovarianceEstimator().Estimate(panel);

        // Renormalize to a correlation matrix: C̃_ij = Σ_ij / sqrt(Σ_ii Σ_jj).
        var diag = new double[n];
        for (var i = 0; i < n; i++)
        {
            diag[i] = Math.Sqrt(Math.Max((double)denoised[i, i], VarianceFloor));
        }

        var c = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (i == j)
                {
                    c[i, j] = 1.0;
                }
                else
                {
                    c[i, j] = (double)denoised[i, j] / (diag[i] * diag[j]);
                }
            }
        }

        return c;
    }

    /// <summary>Averages each off-diagonal pair to remove rounding asymmetry, preserving PSD.</summary>
    private static decimal[,] Symmetrize(decimal[,] m, int n)
    {
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var avg = (m[i, j] + m[j, i]) / 2m;
                m[i, j] = avg;
                m[j, i] = avg;
            }
        }

        return m;
    }
}
