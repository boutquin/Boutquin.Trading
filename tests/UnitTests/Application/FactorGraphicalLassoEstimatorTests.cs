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
/// Fidelity and degenerate-input tests for <see cref="FactorGraphicalLassoEstimator"/>.
///
/// Reference: Lee &amp; Seregina (2020/2023), "Optimal Portfolio Using Factor Graphical
/// Lasso", arXiv 2011.00435. The defining accuracy contract (eq. 3.5 + Thm 3) is that the
/// graphical-lasso residual precision is positive-definite by construction, the
/// Sherman–Morrison–Woodbury low-rank update preserves positive-definiteness, and therefore
/// the returned covariance <c>Σ̂ = Θ̂⁻¹</c> is positive semi-definite.
/// </summary>
public sealed class FactorGraphicalLassoEstimatorTests
{
    private const decimal PsdTolerance = 1e-4m; // numerical dust on a variance scale ~1e-4

    // ═══════════════════════════════════════════════════════════════════════
    // Fidelity — PSD (the M-2 defining property from reference-planned.md)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Estimate_FactorPanel_IsPositiveSemiDefinite()
    {
        // A 4-asset / T=40 / K=1 factor panel (shared factor + idiosyncratic noise).
        // The reassembled precision Θ̂ is positive-definite (GL solution PD by construction,
        // SMW update preserves PD), so Σ̂ = Θ̂⁻¹ is PSD: all eigenvalues ≥ 0.
        var returns = BuildFactorPanel(nAssets: 4, tObs: 40, seed: 11);

        var estimator = new FactorGraphicalLassoEstimator();
        var cov = estimator.Estimate(returns);

        var minEig = MinEigenvalueSymmetric(cov);

        Assert.True(minEig >= -PsdTolerance,
            $"Factor Graphical Lasso estimate must be PSD (eq. 3.5 + GL PD); min eigenvalue = {minEig}");
    }

    [Fact]
    public void Estimate_TwoFactorPanel_IsPositiveSemiDefinite()
    {
        // K=2 factor structure on a 5-asset panel — exercises the K×K SMW inner inversion
        // with K > 1.
        var returns = BuildFactorPanel(nAssets: 5, tObs: 50, seed: 23);

        var estimator = new FactorGraphicalLassoEstimator(numFactors: 2);
        var cov = estimator.Estimate(returns);

        var minEig = MinEigenvalueSymmetric(cov);

        Assert.True(minEig >= -PsdTolerance,
            $"Factor Graphical Lasso (K=2) estimate must be PSD; min eigenvalue = {minEig}");
    }

    [Fact]
    public void Estimate_IsSymmetric()
    {
        var returns = BuildFactorPanel(nAssets: 4, tObs: 40, seed: 11);

        var estimator = new FactorGraphicalLassoEstimator();
        var cov = estimator.Estimate(returns);
        var n = cov.GetLength(0);

        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                Assert.True(Math.Abs(cov[i, j] - cov[j, i]) < 1e-12m,
                    $"Σ̂ must be symmetric at [{i},{j}]: {cov[i, j]} vs {cov[j, i]}");
            }
        }
    }

    [Fact]
    public void Estimate_DiagonalIsPositive()
    {
        // A valid covariance has strictly positive variances on the diagonal.
        var returns = BuildFactorPanel(nAssets: 4, tObs: 40, seed: 11);

        var estimator = new FactorGraphicalLassoEstimator();
        var cov = estimator.Estimate(returns);

        for (var i = 0; i < cov.GetLength(0); i++)
        {
            Assert.True(cov[i, i] > 0m, $"Σ̂[{i},{i}] = {cov[i, i]} must be positive");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Degenerate-input guards
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveK_Throws(int k)
    {
        var act = () => new FactorGraphicalLassoEstimator(numFactors: k);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Estimate_KGreaterOrEqualToAssetCount_ThrowsArgumentException()
    {
        // K must be strictly less than N (the residual space must be non-empty).
        var returns = BuildFactorPanel(nAssets: 2, tObs: 30, seed: 1);

        var estimator = new FactorGraphicalLassoEstimator(numFactors: 2);
        var act = () => estimator.Estimate(returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Estimate_SingleObservation_Throws()
    {
        // T < 2 → caught by SampleCovarianceEstimator.ValidateReturns().
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m } };

        var estimator = new FactorGraphicalLassoEstimator();
        var act = () => estimator.Estimate(returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Estimate_MismatchedLengths_Throws()
    {
        var returns = new[] { new[] { 0.01m, 0.02m, 0.03m }, new[] { 0.02m, 0.01m } };

        var estimator = new FactorGraphicalLassoEstimator();
        var act = () => estimator.Estimate(returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Estimate_ZeroVarianceAsset_ThrowsCalculationException()
    {
        // A constant series has σ = 0 → the factor model and the GL solve are degenerate.
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m },
            new[] { 0.02m, 0.01m, -0.01m, 0.02m },
            new[] { 0.05m, 0.05m, 0.05m, 0.05m }, // constant → σ = 0
        };

        var estimator = new FactorGraphicalLassoEstimator();
        var act = () => estimator.Estimate(returns);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void Estimate_NullReturns_Throws()
    {
        var estimator = new FactorGraphicalLassoEstimator();
        var act = () => estimator.Estimate(null!);
        act.Should().Throw<ArgumentException>();
    }

    // ─── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a deterministic K=1 latent-factor panel: a shared factor plus idiosyncratic
    /// noise, returned as a jagged <c>N × T</c> array. Uses a fixed-seed LCG so the test is
    /// reproducible and has no Python dependency.
    /// </summary>
    private static decimal[][] BuildFactorPanel(int nAssets, int tObs, int seed)
    {
        var rng = new SimpleRng(seed);
        var factor = new double[tObs];
        for (var s = 0; s < tObs; s++)
        {
            factor[s] = rng.NextGaussian() * 0.012;
        }

        var returns = new decimal[nAssets][];
        for (var i = 0; i < nAssets; i++)
        {
            var loading = 0.5 + (rng.NextDouble() * 1.0);
            var series = new decimal[tObs];
            for (var s = 0; s < tObs; s++)
            {
                var value = (loading * factor[s]) + (rng.NextGaussian() * 0.006);
                series[s] = (decimal)Math.Round(value, 6);
            }

            returns[i] = series;
        }

        return returns;
    }

    private sealed class SimpleRng
    {
        private ulong _state;

        public SimpleRng(int seed) => _state = (ulong)(seed + 1) * 0x9E3779B97F4A7C15UL;

        public double NextDouble()
        {
            // SplitMix64.
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / 9007199254740992.0);
        }

        public double NextGaussian()
        {
            // Box–Muller.
            var u1 = Math.Max(NextDouble(), 1e-12);
            var u2 = NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }

    private static decimal MinEigenvalueSymmetric(decimal[,] matrix)
    {
        var n = matrix.GetLength(0);
        var a = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                a[i, j] = (double)matrix[i, j];
            }
        }

        for (var sweep = 0; sweep < 100; sweep++)
        {
            var off = 0.0;
            for (var p = 0; p < n; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    off += a[p, q] * a[p, q];
                }
            }

            if (off < 1e-20)
            {
                break;
            }

            for (var p = 0; p < n; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-300)
                    {
                        continue;
                    }

                    var theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                    var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt((theta * theta) + 1.0));
                    if (theta == 0.0)
                    {
                        t = 1.0;
                    }

                    var cos = 1.0 / Math.Sqrt((t * t) + 1.0);
                    var sin = t * cos;
                    for (var i = 0; i < n; i++)
                    {
                        var aip = a[i, p];
                        var aiq = a[i, q];
                        a[i, p] = (cos * aip) - (sin * aiq);
                        a[i, q] = (sin * aip) + (cos * aiq);
                    }

                    for (var i = 0; i < n; i++)
                    {
                        var api = a[p, i];
                        var aqi = a[q, i];
                        a[p, i] = (cos * api) - (sin * aqi);
                        a[q, i] = (sin * api) + (cos * aqi);
                    }
                }
            }
        }

        var min = double.PositiveInfinity;
        for (var i = 0; i < n; i++)
        {
            min = Math.Min(min, a[i, i]);
        }

        return (decimal)min;
    }
}
