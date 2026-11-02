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
/// Fidelity tests for <see cref="DccNlCovarianceEstimator"/> — the DCC-NL
/// (Engle, Ledoit &amp; Wolf 2019) time-varying covariance model.
///
/// Verifies the defining accuracy contract from <c>reference-planned.md</c>:
/// every per-date conditional covariance matrix H_t is positive semi-definite
/// when the DCC stationarity condition α+β&lt;1 holds, and the stationarity guard
/// rejects α+β≥1.
/// </summary>
public sealed class DccNlCovarianceEstimatorTests
{
    private const double PsdEigenvalueTolerance = -1e-9;

    /// <summary>
    /// Generates a deterministic N-asset, T-observation synthetic return panel
    /// (jagged: returns[asset][time]) with mild cross-correlation and volatility
    /// clustering, so GARCH(1,1) fitting is well-posed.
    /// </summary>
    private static decimal[][] SyntheticReturns(int n, int t, int seed = 7)
    {
        var rng = new Random(seed);
        var panel = new decimal[n][];
        // A shared "market" factor induces correlation across assets.
        var market = new double[t];
        for (var k = 0; k < t; k++)
        {
            market[k] = 0.008 * (rng.NextDouble() - 0.5);
        }

        for (var i = 0; i < n; i++)
        {
            var series = new double[t];
            var vol = 0.01;
            for (var k = 0; k < t; k++)
            {
                // GARCH-like volatility clustering.
                vol = 0.0008 + 0.85 * vol + 0.1 * Math.Abs(series[Math.Max(0, k - 1)]);
                var idio = vol * (rng.NextDouble() - 0.5);
                series[k] = 0.6 * market[k] + idio;
            }

            panel[i] = series.Select(x => (decimal)x).ToArray();
        }

        return panel;
    }

    /// <summary>Computes the smallest eigenvalue of a symmetric matrix via power-iteration on a shifted matrix.</summary>
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

        // Gershgorin upper bound for the spectral radius shift.
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

        // B = shift*I - A  has largest eigenvalue = shift - lambda_min(A).
        var b = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                b[i, j] = (i == j ? shift : 0.0) - a[i, j];
            }
        }

        var v = new double[n];
        for (var i = 0; i < n; i++)
        {
            v[i] = 1.0;
        }

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

    [Fact]
    public void GetConditionalCovariances_AllDates_AreSymmetricPsd()
    {
        var returns = SyntheticReturns(n: 3, t: 80);
        IDynamicCovarianceEstimator estimator = new DccNlCovarianceEstimator();

        estimator.Fit(returns);
        var covariances = estimator.GetConditionalCovariances();

        covariances.Should().NotBeEmpty();
        foreach (var h in covariances)
        {
            // Symmetry.
            var n = h.GetLength(0);
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    h[i, j].Should().BeApproximately(h[j, i], 1e-18m);
                }
            }

            // PSD: smallest eigenvalue ≥ 0 (within tolerance).
            SmallestEigenvalue(h).Should().BeGreaterThanOrEqualTo(PsdEigenvalueTolerance);
        }
    }

    [Fact]
    public void StationaryParameters_ImplyPsd_ForecastIsPsd()
    {
        var returns = SyntheticReturns(n: 4, t: 100);
        // α+β = 0.98 < 1 — stationary.
        IDynamicCovarianceEstimator estimator = new DccNlCovarianceEstimator(alpha: 0.02m, beta: 0.96m);

        estimator.Fit(returns);
        var forecast = estimator.Forecast();

        SmallestEigenvalue(forecast).Should().BeGreaterThanOrEqualTo(PsdEigenvalueTolerance);
    }

    [Fact]
    public void Constructor_NonStationaryParameters_Throws()
    {
        // α+β = 1.0 ≥ 1 violates the stationarity condition.
        var act = () => new DccNlCovarianceEstimator(alpha: 0.5m, beta: 0.5m);
        act.Should().Throw<ArgumentOutOfRangeException>();

        // α+β > 1 likewise.
        var act2 = () => new DccNlCovarianceEstimator(alpha: 0.6m, beta: 0.5m);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fit_FewerThanTwoAssets_ThrowsArgumentException()
    {
        var returns = SyntheticReturns(n: 1, t: 50);
        IDynamicCovarianceEstimator estimator = new DccNlCovarianceEstimator();

        var act = () => estimator.Fit(returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fit_FewerThanTenObservations_ThrowsArgumentException()
    {
        var returns = SyntheticReturns(n: 2, t: 9);
        IDynamicCovarianceEstimator estimator = new DccNlCovarianceEstimator();

        var act = () => estimator.Fit(returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fit_NearZeroVarianceAsset_ThrowsCalculationException()
    {
        var returns = SyntheticReturns(n: 3, t: 60);
        // Make asset 1 a (near-)constant series.
        for (var k = 0; k < returns[1].Length; k++)
        {
            returns[1][k] = 0.0001m;
        }

        IDynamicCovarianceEstimator estimator = new DccNlCovarianceEstimator();

        var act = () => estimator.Fit(returns);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void GetConditionalCovariances_BeforeFit_Throws()
    {
        IDynamicCovarianceEstimator estimator = new DccNlCovarianceEstimator();
        var act = estimator.GetConditionalCovariances;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Forecast_BeforeFit_Throws()
    {
        IDynamicCovarianceEstimator estimator = new DccNlCovarianceEstimator();
        var act = estimator.Forecast;
        act.Should().Throw<InvalidOperationException>();
    }
}
