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

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for the DCC-NL <c>Fit()</c> wiring of two-step QMLE parameter estimation (spec
/// <c>feature-dcc-nl-parameter-estimation</c> M-3): the end-to-end round-trip under the
/// estimating default (AC 3-1) and the <c>estimateParameters: false</c> opt-out equivalence
/// with the fixed-parameter V1 behavior (AC 3-2), verified two ways — a bit-for-bit equality
/// against a direct <see cref="DccNlCovarianceEstimator.RunDccRecursion"/> call, and the current forecast variance/symmetry/PSD identities.
/// </summary>
public sealed class DccNlOptOutEquivalenceTests : CrossLanguageVerificationBase
{
    private const double PsdEigenvalueTolerance = -1e-9;

    /// <summary>
    /// AC 3-1 — end-to-end round-trip: a default-constructed estimator (estimation on) fits a
    /// 5-asset, 60-observation panel without throwing and forecasts a 5×5 PSD covariance.
    /// </summary>
    [Fact]
    public void Fit_DefaultConstructor_FiveAssetPanel_ReturnsPsdFiveByFiveForecast()
    {
        var panel = SyntheticPanel(n: 5, t: 60, seed: 11);

        var estimator = new DccNlCovarianceEstimator(); // default: estimateParameters = true.
        estimator.Fit(panel);
        var forecast = estimator.Forecast();

        forecast.GetLength(0).Should().Be(5);
        forecast.GetLength(1).Should().Be(5);
        SmallestEigenvalue(forecast).Should().BeGreaterThanOrEqualTo(PsdEigenvalueTolerance);
    }

    /// <summary>
    /// AC 3-2 (way 1) — direct-recursion equality: the <c>estimateParameters: false</c> forecast
    /// is entry-for-entry equal (within 1e-18) to a direct
    /// <see cref="DccNlCovarianceEstimator.RunDccRecursion"/> call on the same (C̃, s), proving the
    /// opt-out path never invokes <see cref="DccNlCovarianceEstimator.EstimateDccParameters"/>.
    /// </summary>
    [Fact]
    public void OptOut_Forecast_EqualsDirectRunDccRecursion()
    {
        var panel = SyntheticPanel(n: 5, t: 60, seed: 11);

        var (s, d2, oneStepVar, cTilde, n, t) = DccNlCovarianceEstimator.PreprocessReturns(panel);
        var (_, directForecast) =
            DccNlCovarianceEstimator.RunDccRecursion(0.01, 0.97, cTilde, s, d2, oneStepVar, n, t);

        var estimator = new DccNlCovarianceEstimator(0.01m, 0.97m, estimateParameters: false);
        estimator.Fit(panel);
        var forecast = estimator.Forecast();

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                forecast[i, j].Should().BeApproximately(directForecast[i, j], 1e-18m);
            }
        }
    }

    /// <summary>
    /// Fixed DCC parameters preserve the conditional covariance identities while upstream
    /// GARCH fitting may improve; a historical forecast literal is not the mathematical contract.
    /// </summary>
    [Fact]
    public void OptOut_CurrentGarchForecastMatchesVarianceAndPsdIdentities()
    {
        var panel = SyntheticPanel(n: 5, t: 60, seed: 11);
        var (_, _, oneStepVar, _, n, _) = DccNlCovarianceEstimator.PreprocessReturns(panel);
        var estimator = new DccNlCovarianceEstimator(0.01m, 0.97m, estimateParameters: false);
        estimator.Fit(panel);
        var forecast = estimator.Forecast();
        for (var i = 0; i < n; i++)
        {
            forecast[i, i].Should().BeApproximately((decimal)oneStepVar[i], 1e-18m,
                "a conditional covariance diagonal equals the fitted univariate forecast variance");
            for (var j = 0; j < n; j++)
            {
                forecast[i, j].Should().Be(forecast[j, i]);
            }
        }
        SmallestEigenvalue(forecast).Should().BeGreaterThanOrEqualTo(PsdEigenvalueTolerance);
    }

    /// <summary>
    /// Generates a deterministic N-asset, T-observation return panel (jagged
    /// <c>returns[asset][time]</c>) with a shared market factor and GARCH-like volatility
    /// clustering, so the per-asset GARCH(1,1) fit is well-posed.
    /// </summary>
    private static decimal[][] SyntheticPanel(int n, int t, int seed)
    {
        var rng = new Random(seed);
        var market = new double[t];
        for (var k = 0; k < t; k++)
        {
            market[k] = 0.008 * (rng.NextDouble() - 0.5);
        }

        var panel = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            var series = new double[t];
            var vol = 0.01;
            for (var k = 0; k < t; k++)
            {
                vol = 0.0008 + (0.85 * vol) + (0.1 * Math.Abs(series[Math.Max(0, k - 1)]));
                var idio = vol * (rng.NextDouble() - 0.5);
                series[k] = (0.6 * market[k]) + idio;
            }

            panel[i] = series.Select(x => (decimal)x).ToArray();
        }

        return panel;
    }

    /// <summary>Smallest eigenvalue of a symmetric matrix via power iteration on a Gershgorin-shifted matrix.</summary>
    private static double SmallestEigenvalue(decimal[,] m)
    {
        var n = m.GetLength(0);
        var a = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                a[i, j] = (double)m[i, j];
            }
        }

        var shift = 0.0;
        for (var i = 0; i < n; i++)
        {
            var row = 0.0;
            for (var j = 0; j < n; j++)
            {
                row += Math.Abs(a[i, j]);
            }

            shift = Math.Max(shift, row);
        }

        var b = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                b[i, j] = (i == j ? shift : 0.0) - a[i, j];
            }
        }

        var v = Enumerable.Repeat(1.0, n).ToArray();
        var lambda = 0.0;
        for (var iter = 0; iter < 1000; iter++)
        {
            var w = new double[n];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    w[i] += b[i, j] * v[j];
                }
            }

            var norm = Math.Sqrt(w.Sum(x => x * x));
            if (norm < 1e-300)
            {
                break;
            }

            for (var i = 0; i < n; i++)
            {
                w[i] /= norm;
            }

            lambda = norm;
            v = w;
        }

        return shift - lambda;
    }
}
