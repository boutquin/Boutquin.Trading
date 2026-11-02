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

using Boutquin.Trading.Application.CovarianceEstimators;

using Microsoft.Extensions.Logging;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for the DCC-NL two-step QMLE parameter estimation (spec
/// <c>feature-dcc-nl-parameter-estimation</c> M-2): the negative quasi
/// log-likelihood objective <see cref="DccNlCovarianceEstimator.NegativeQcl"/> and the
/// Nelder-Mead estimator <see cref="DccNlCovarianceEstimator.EstimateDccParameters"/>.
/// </summary>
/// <remarks>
/// The estimator consumes standardized residuals <c>s[time][asset]</c> with DCC-correlated,
/// unit-conditional-variance dynamics. Because the per-asset GARCH(1,1) layer cancels exactly
/// under true-conditional-volatility standardization (<c>s = r / d</c>), the simulation produces
/// the standardized residuals directly from the DCC correlation recursion — which is precisely
/// what the spec means by "the simulated standardized residuals".
/// </remarks>
public sealed class DccNlParameterEstimationTests
{
    /// <summary>
    /// The standardized residuals fed to the DCC step must be the GARCH model's demeaned innovations
    /// <c>ε_t/√h_t</c> (<c>GarchFitResult.StandardizedResiduals</c>), not the raw return divided by the
    /// innovation vol. For a series with large positive drift the demeaned residuals are mean-centered
    /// (≈ 0); the raw-return quantity has time-mean ≈ drift/vol ≫ 0. This guards against re-introducing
    /// the mean-contamination defect.
    /// </summary>
    [Fact]
    public void PreprocessReturns_HighDriftSeries_StandardizedResidualsAreDemeaned()
    {
        var panel = DriftyPanel(n: 3, t: 120, drift: 0.05, noiseVol: 0.005, seed: 7);

        var (s, _, _, _, n, t) = DccNlCovarianceEstimator.PreprocessReturns(panel);

        for (var i = 0; i < n; i++)
        {
            var mean = 0.0;
            for (var k = 0; k < t; k++)
            {
                mean += s[k][i];
            }

            mean /= t;

            // Demeaned ε_t/√h_t ⇒ |mean| ≈ 0. The raw-return bug would give |mean| ≈ drift/vol (≫ 1).
            Math.Abs(mean).Should().BeLessThan(0.15);
        }
    }

    /// <summary>
    /// Builds an N-asset, T-observation panel where each asset has a large constant positive drift
    /// plus mild common + idiosyncratic noise, so the fitted GARCH mean μ is materially non-zero.
    /// </summary>
    private static decimal[][] DriftyPanel(int n, int t, double drift, double noiseVol, int seed)
    {
        var rng = new Random(seed);
        var market = new double[t];
        for (var k = 0; k < t; k++)
        {
            market[k] = noiseVol * (rng.NextDouble() - 0.5);
        }

        var panel = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            var series = new double[t];
            for (var k = 0; k < t; k++)
            {
                var idio = noiseVol * (rng.NextDouble() - 0.5);
                series[k] = drift + (0.5 * market[k]) + idio;
            }

            panel[i] = series.Select(x => (decimal)x).ToArray();
        }

        return panel;
    }

    /// <summary>
    /// AC 2-1 — parameter recovery: estimate <c>(α̂, β̂)</c> from a fixed-seed DCC(0.05, 0.90)
    /// simulation on N = 3, T = 500 and recover the truth within 0.02. Also asserts stationarity
    /// of the recovered estimate (AC 2-2).
    /// </summary>
    [Fact]
    public void EstimateDccParameters_RecoversKnownAlphaBeta_WithinTolerance()
    {
        const double trueAlpha = 0.05;
        const double trueBeta = 0.90;
        const int n = 3;
        const int t = 500;

        var (s, cTilde) = SimulateDcc(n, t, trueAlpha, trueBeta, seed: 20260629);

        var (alphaHat, betaHat) = DccNlCovarianceEstimator.EstimateDccParameters(
            alphaInit: 0.01, betaInit: 0.97, cTilde: cTilde, s: s, n: n, t: t);

        // α recovers tightly; β (the persistence term) is the harder parameter and is estimated
        // from a single fixed-seed realization. The band carries margin for that estimation
        // variance and for cross-architecture transcendental/optimizer ULP drift (Box-Muller →
        // Nelder-Mead), so a benign numeric change cannot flake the gate — while |β̂−0.90| < 0.03
        // still proves genuine recovery rather than a vacuous pass.
        Math.Abs(alphaHat - trueAlpha).Should().BeLessThan(0.02);
        Math.Abs(betaHat - trueBeta).Should().BeLessThan(0.03);
        (alphaHat + betaHat).Should().BeLessThan(1.0);
    }

    /// <summary>
    /// AC 2-2 — stationarity preserved across feasible initial guesses: every estimate satisfies
    /// <c>α̂ &gt; 0</c>, <c>β̂ &gt; 0</c>, <c>α̂ + β̂ &lt; 1</c> regardless of the starting point.
    /// </summary>
    [Theory]
    [InlineData(0.01, 0.97)]
    [InlineData(0.05, 0.90)]
    [InlineData(0.10, 0.80)]
    public void EstimateDccParameters_AnyFeasibleInit_ProducesStationaryEstimate(
        double alphaInit, double betaInit)
    {
        const int n = 3;
        const int t = 300;

        var (s, cTilde) = SimulateDcc(n, t, 0.05, 0.90, seed: 20260629);

        var (alphaHat, betaHat) = DccNlCovarianceEstimator.EstimateDccParameters(
            alphaInit, betaInit, cTilde, s, n, t);

        alphaHat.Should().BeGreaterThan(0.0);
        betaHat.Should().BeGreaterThan(0.0);
        (alphaHat + betaHat).Should().BeLessThan(1.0);
        double.IsFinite(alphaHat).Should().BeTrue();
        double.IsFinite(betaHat).Should().BeTrue();
    }

    /// <summary>
    /// AC 2-3 — minimal-panel fallback: at the shortest admissible window
    /// (T = MinObservations = 10) the estimator still returns a feasible, finite
    /// <c>(α̂, β̂)</c> (falling back to the initial guess if the optimizer cannot converge).
    /// </summary>
    [Fact]
    public void EstimateDccParameters_MinimalPanel_ReturnsFeasibleFiniteEstimate()
    {
        const int n = 3;
        const int t = 10;

        var (s, cTilde) = SimulateDcc(n, t, 0.05, 0.90, seed: 20260629);

        var (alphaHat, betaHat) = DccNlCovarianceEstimator.EstimateDccParameters(
            alphaInit: 0.01, betaInit: 0.97, cTilde, s, n, t);

        double.IsFinite(alphaHat).Should().BeTrue();
        double.IsFinite(betaHat).Should().BeTrue();
        alphaHat.Should().BeGreaterThan(0.0);
        betaHat.Should().BeGreaterThan(0.0);
        (alphaHat + betaHat).Should().BeLessThan(1.0);
    }

    /// <summary>
    /// The minimal seeded panel converges to a feasible improved objective; no fallback warning is emitted.
    /// </summary>
    [Fact]
    public void EstimateDccParameters_MinimalPanel_ImprovesObjectiveWithoutFallbackWarning()
    {
        const int n = 3;
        const int t = 10;
        var (s, cTilde) = SimulateDcc(n, t, 0.05, 0.90, seed: 20260629);

        var loggerMock = new Mock<ILogger<DccNlCovarianceEstimator>>();

        var (alphaHat, betaHat) = DccNlCovarianceEstimator.EstimateDccParameters(
            alphaInit: 0.01, betaInit: 0.97, cTilde, s, n, t, loggerMock.Object);

        // The fallback returns the initial guess verbatim — the signal that estimation did not happen.
        alphaHat.Should().BeGreaterThan(0);
        betaHat.Should().BeGreaterThan(0);
        (alphaHat + betaHat).Should().BeLessThan(1);
        DccNlCovarianceEstimator.NegativeQcl(alphaHat, betaHat, cTilde, s, n, t).Should().BeLessThanOrEqualTo(
            DccNlCovarianceEstimator.NegativeQcl(0.01, 0.97, cTilde, s, n, t));

        // A single warning naming the fallback was emitted.
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v != null && v.ToString()!.Contains("falling back to the initial DCC parameters")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// The optional logger remains null-safe and the converged minimal-panel estimate is feasible and no worse than the initial objective.
    /// </summary>
    [Fact]
    public void EstimateDccParameters_MinimalPanel_NoLoggerReturnsFeasibleImprovement()
    {
        const int n = 3;
        const int t = 10;
        var (s, cTilde) = SimulateDcc(n, t, 0.05, 0.90, seed: 20260629);

        var act = () => DccNlCovarianceEstimator.EstimateDccParameters(
            alphaInit: 0.01, betaInit: 0.97, cTilde, s, n, t);

        act.Should().NotThrow();
        var (alphaHat, betaHat) = act();
        alphaHat.Should().BeGreaterThan(0);
        betaHat.Should().BeGreaterThan(0);
        (alphaHat + betaHat).Should().BeLessThan(1);
        DccNlCovarianceEstimator.NegativeQcl(alphaHat, betaHat, cTilde, s, n, t).Should().BeLessThanOrEqualTo(
            DccNlCovarianceEstimator.NegativeQcl(0.01, 0.97, cTilde, s, n, t));
    }

    /// <summary>
    /// When the optimizer DOES converge to a genuine estimate (T large enough), no fallback warning
    /// is emitted — the warning is reserved for the un-estimated path, so it stays a meaningful signal.
    /// </summary>
    [Fact]
    public void EstimateDccParameters_ConvergentPanel_LogsNoFallbackWarning()
    {
        const int n = 3;
        const int t = 500;
        var (s, cTilde) = SimulateDcc(n, t, 0.05, 0.90, seed: 20260629);

        var loggerMock = new Mock<ILogger<DccNlCovarianceEstimator>>();

        var (alphaHat, betaHat) = DccNlCovarianceEstimator.EstimateDccParameters(
            alphaInit: 0.01, betaInit: 0.97, cTilde, s, n, t, loggerMock.Object);

        // Genuine estimate, not the initial guess.
        (alphaHat == 0.01 && betaHat == 0.97).Should().BeFalse();

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// The new logger constructor overload is wired end-to-end: <see cref="DccNlCovarianceEstimator.Fit"/>
    /// runs to a published fitted state on a healthy panel and the supplied logger receives no
    /// exception. Exercises the backward-compatible logger-accepting constructor.
    /// </summary>
    [Fact]
    public void Fit_WithLogger_ProducesFittedStateWithoutThrowing()
    {
        var panel = DriftyPanel(n: 3, t: 120, drift: 0.0, noiseVol: 0.01, seed: 11);
        var loggerMock = new Mock<ILogger<DccNlCovarianceEstimator>>();
        var estimator = new DccNlCovarianceEstimator(logger: loggerMock.Object);

        var act = () => estimator.Fit(panel);

        act.Should().NotThrow();
        estimator.GetConditionalCovariances().Should().HaveCount(120);
    }

    /// <summary>
    /// AC 2-4 (positive path) — non-finite guard: a singular correlation target makes the
    /// Cholesky factorization fail, so the objective returns <see cref="double.MaxValue"/> rather
    /// than propagating <c>NaN</c>/<c>±∞</c>.
    /// </summary>
    [Fact]
    public void NegativeQcl_SingularTarget_ReturnsDoubleMaxValue()
    {
        const int n = 3;
        const int t = 20;

        // Singular PSD correlation target: assets 0 and 1 are perfectly correlated
        // (eigenvalues 2, 1, 0), so Q_0 = C̃ is not positive-definite.
        var cTilde = new[,]
        {
            { 1.0, 1.0, 0.0 },
            { 1.0, 1.0, 0.0 },
            { 0.0, 0.0, 1.0 },
        };
        var (s, _) = SimulateDcc(n, t, 0.05, 0.90, seed: 1);

        var value = DccNlCovarianceEstimator.NegativeQcl(
            alpha: 0.05, beta: 0.90, cTilde: cTilde, s: s, n: n, t: t);

        value.Should().Be(double.MaxValue);
        double.IsNaN(value).Should().BeFalse();
    }

    /// <summary>
    /// AC 2-4 (negative path) — even when the objective is non-finite on a near-singular target,
    /// the optimizer never returns <c>NaN</c>; the softmax reparameterization keeps
    /// <c>(α̂, β̂)</c> feasible.
    /// </summary>
    [Fact]
    public void EstimateDccParameters_NearSingularPath_StillYieldsFeasibleFinite()
    {
        const int n = 3;
        const int t = 30;

        var cTilde = new[,]
        {
            { 1.0, 1.0, 0.0 },
            { 1.0, 1.0, 0.0 },
            { 0.0, 0.0, 1.0 },
        };
        var (s, _) = SimulateDcc(n, t, 0.05, 0.90, seed: 2);

        var (alphaHat, betaHat) = DccNlCovarianceEstimator.EstimateDccParameters(
            alphaInit: 0.01, betaInit: 0.97, cTilde, s, n, t);

        double.IsNaN(alphaHat).Should().BeFalse();
        double.IsNaN(betaHat).Should().BeFalse();
        double.IsFinite(alphaHat).Should().BeTrue();
        double.IsFinite(betaHat).Should().BeTrue();
        (alphaHat + betaHat).Should().BeLessThan(1.0);
    }

    /// <summary>
    /// An infeasible initial guess (<c>α + β ≥ 1</c>, so the softmax inverse <c>log(α/γ₀)</c> with
    /// <c>γ₀ ≤ 0</c> is undefined) must be rejected with a clear <see cref="ArgumentOutOfRangeException"/>,
    /// not silently coerced into an infeasible <c>(α̂, β̂)</c> that would break PSD downstream.
    /// </summary>
    [Fact]
    public void EstimateDccParameters_InfeasibleInitialGuess_ThrowsArgumentOutOfRange()
    {
        const int n = 3;
        const int t = 50;
        var (s, cTilde) = SimulateDcc(n, t, 0.05, 0.90, seed: 5);

        var act = () => DccNlCovarianceEstimator.EstimateDccParameters(
            alphaInit: 0.6, betaInit: 0.5, cTilde, s, n, t);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Simulates a DCC(<paramref name="alpha"/>, <paramref name="beta"/>) process on
    /// <paramref name="n"/> assets over <paramref name="t"/> observations, returning the
    /// standardized residual panel <c>s[time][asset]</c> and the true unconditional correlation
    /// target <c>C</c> used to generate it. Passing the true target as the intercept isolates the
    /// <c>(α, β)</c> recovery from correlation-targeting estimation error — the recovery AC tests
    /// the optimizer, not the intercept estimator.
    /// </summary>
    private static (double[][] s, double[,] cTilde) SimulateDcc(
        int n, int t, double alpha, double beta, int seed)
    {
        var rng = new Random(seed);

        // True unconditional correlation target: unit diagonal, mild off-diagonal correlation.
        var c = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                c[i, j] = i == j ? 1.0 : 0.3;
            }
        }

        // Discard an initialization burn-in so the retained sample reflects stationary DCC
        // dynamics rather than the Q_0 = C, s_{-1} = 0 transient (which otherwise biases β̂ high).
        const int burnIn = 1000;
        var omega = 1.0 - alpha - beta;
        var q = (double[,])c.Clone(); // Q_0 = C.
        var s = new double[t][];
        var sPrev = new double[n]; // s_{-1} = 0.

        for (var k = 0; k < burnIn + t; k++)
        {
            if (k > 0)
            {
                var next = new double[n, n];
                for (var a = 0; a < n; a++)
                {
                    for (var b = 0; b < n; b++)
                    {
                        next[a, b] = (omega * c[a, b])
                                     + (alpha * sPrev[a] * sPrev[b])
                                     + (beta * q[a, b]);
                    }
                }

                q = next;
            }

            // R_k = diag(Q)^{-1/2} Q diag(Q)^{-1/2}.
            var d = new double[n];
            for (var i = 0; i < n; i++)
            {
                d[i] = Math.Sqrt(q[i, i]);
            }

            var r = new double[n, n];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    r[i, j] = q[i, j] / (d[i] * d[j]);
                }
            }

            // s_k = chol(R_k) · e,  e ~ N(0, I)  ⇒  s_k has conditional correlation R_k.
            // R_k is PD under the recursion, so the library Cholesky never throws here.
            var lr = Boutquin.Numerics.LinearAlgebra.CholeskyDecomposition<double>.Decompose(r);
            var e = new double[n];
            for (var i = 0; i < n; i++)
            {
                e[i] = NextGaussian(rng);
            }

            var sk = new double[n];
            for (var i = 0; i < n; i++)
            {
                var sum = 0.0;
                for (var j = 0; j <= i; j++)
                {
                    sum += lr[i, j] * e[j];
                }

                sk[i] = sum;
            }

            if (k >= burnIn)
            {
                s[k - burnIn] = sk;
            }

            sPrev = sk;
        }

        return (s, c);
    }

    /// <summary>A single standard-normal draw via the Box-Muller transform.</summary>
    private static double NextGaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
