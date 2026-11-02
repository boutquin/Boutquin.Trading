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

using System.Text.Json;

using Boutquin.Trading.Application.CovarianceEstimators;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification and fidelity tests for <see cref="GerberCovarianceEstimator"/>.
///
/// Validates the C# Gerber Statistic 2 implementation against own-formula Python reference
/// vectors (tests/Verification/vectors/gerber_covariance_*.json, from
/// generate_gerber_covariance_vectors.py — NOT an external Gerber library).
///
/// Reference: Gerber, Markowitz, Ernst, Miao, Javid &amp; Sargen (2023),
/// "Proofs that the Gerber Statistic is Positive Semidefinite", arXiv 2305.05663.
/// </summary>
public sealed class GerberCovarianceCrossLanguageTests : CrossLanguageVerificationBase
{
    // ─── Helpers ────────────────────────────────────────────────────────

    private static decimal[][] GetJaggedArray(JsonElement element)
    {
        var outer = element.EnumerateArray().ToArray();
        var result = new decimal[outer.Length][];
        for (var i = 0; i < outer.Length; i++)
        {
            result[i] = outer[i].EnumerateArray()
                .Select(e => (decimal)e.GetDouble())
                .ToArray();
        }
        return result;
    }

    private static decimal[,] GetMatrix(JsonElement element)
    {
        var rows = element.EnumerateArray().ToArray();
        var n = rows.Length;
        var m = rows[0].EnumerateArray().Count();
        var result = new decimal[n, m];
        for (var i = 0; i < n; i++)
        {
            var cols = rows[i].EnumerateArray().ToArray();
            for (var j = 0; j < m; j++)
            {
                result[i, j] = (decimal)cols[j].GetDouble();
            }
        }
        return result;
    }

    private static void AssertMatrixEqual(decimal[,] expected, decimal[,] actual, decimal tolerance, string label = "")
    {
        Assert.Equal(expected.GetLength(0), actual.GetLength(0));
        Assert.Equal(expected.GetLength(1), actual.GetLength(1));

        for (var i = 0; i < expected.GetLength(0); i++)
        {
            for (var j = 0; j < expected.GetLength(1); j++)
            {
                AssertWithinTolerance(actual[i, j], expected[i, j], tolerance, $"{label}[{i},{j}]: ");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-2: Cross-language vectors (own-formula, PrecisionNumeric)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GerberCovariance_3Asset_MatchesPythonOwnFormula()
    {
        using var doc = LoadVector("gerber_covariance_3asset");
        var returns = GetJaggedArray(doc.RootElement.GetProperty("inputs").GetProperty("returns"));
        var expectedCov = GetMatrix(doc.RootElement.GetProperty("expected").GetProperty("gerber_covariance"));

        var estimator = new GerberCovarianceEstimator();
        var result = estimator.Estimate(returns);

        AssertMatrixEqual(expectedCov, result, PrecisionNumeric, "GerberCov3x3");
    }

    [Fact]
    public void GerberCovariance_5Asset_MatchesPythonOwnFormula()
    {
        using var doc = LoadVector("gerber_covariance_5asset");
        var returns = GetJaggedArray(doc.RootElement.GetProperty("inputs").GetProperty("returns"));
        var expectedCov = GetMatrix(doc.RootElement.GetProperty("expected").GetProperty("gerber_covariance"));

        var estimator = new GerberCovarianceEstimator();
        var result = estimator.Estimate(returns);

        AssertMatrixEqual(expectedCov, result, PrecisionNumeric, "GerberCov5x5");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-5: c parameter — c=0.25 produces the c=0.25 reference matrix
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GerberCovariance_C025_MatchesPythonOwnFormula()
    {
        using var doc = LoadVector("gerber_covariance_c025");
        var returns = GetJaggedArray(doc.RootElement.GetProperty("inputs").GetProperty("returns"));
        var expectedCov = GetMatrix(doc.RootElement.GetProperty("expected").GetProperty("gerber_covariance"));

        var estimator = new GerberCovarianceEstimator(c: 0.25m);
        var result = estimator.Estimate(returns);

        AssertMatrixEqual(expectedCov, result, PrecisionNumeric, "GerberCovC025");
    }

    [Fact]
    public void GerberCovariance_DefaultC_IsOneHalf()
    {
        // The default ctor (c=0.5) and an explicit c=0.5 must produce identical results
        // against the c=0.5 reference panel — pins the canonical default.
        using var doc = LoadVector("gerber_covariance_3asset");
        var returns = GetJaggedArray(doc.RootElement.GetProperty("inputs").GetProperty("returns"));

        var defaultEstimator = new GerberCovarianceEstimator();
        var explicitEstimator = new GerberCovarianceEstimator(c: 0.5m);

        AssertMatrixEqual(defaultEstimator.Estimate(returns), explicitEstimator.Estimate(returns),
            PrecisionExact, "DefaultVsExplicitHalf");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    public void GerberCovariance_NonPositiveC_Throws(double c)
    {
        var act = () => new GerberCovarianceEstimator(c: (decimal)c);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-3: Fidelity — PSD and the rescale identity Sigma_ii = sigma_i^2
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GerberCovariance_5Asset_IsPositiveSemiDefinite()
    {
        // Prop. 2 (arXiv 2305.05663): Gerber Statistic 2 is PSD. In exact arithmetic
        // all eigenvalues are >= 0; finite-precision construction (decimal→double, sqrt)
        // leaves dust at the ~1e-5 scale around zero on a variance scale of ~1e-4. We
        // assert the minimum eigenvalue is non-negative up to PrecisionStatistical — the
        // repo's documented eigendecomposition tolerance — AND that it agrees with the
        // Python/LAPACK reference minimum eigenvalue (the independent PSD ground truth).
        using var doc = LoadVector("gerber_covariance_5asset");
        var returns = GetJaggedArray(doc.RootElement.GetProperty("inputs").GetProperty("returns"));
        var referenceMinEig = GetDecimal(doc.RootElement.GetProperty("expected"), "min_eigenvalue");

        var estimator = new GerberCovarianceEstimator();
        var cov = estimator.Estimate(returns);

        var minEig = MinEigenvalueSymmetric(cov);

        // PSD up to numerical dust (Prop. 2).
        Assert.True(minEig >= -PrecisionStatistical,
            $"Gerber-2 estimate must be PSD (Prop. 2); min eigenvalue = {minEig}");

        // Agrees with the LAPACK reference minimum eigenvalue — proves the dust is shared
        // numerical behavior, not a C#-specific PSD violation.
        AssertWithinTolerance(minEig, referenceMinEig, PrecisionStatistical, "min eigenvalue vs LAPACK: ");
    }

    [Fact]
    public void GerberCovariance_IndefiniteUnderBothLargeDenominator_IsPositiveSemiDefinite()
    {
        // PSD fidelity counterexample (Prop. 2, arXiv 2305.05663). This n=5/T=26/c=0.5
        // panel (numpy default_rng seed 52, returns rounded to 4 decimals) is INDEFINITE
        // under the both-large denominator (Gerber Statistic 1: denom = N^UU+N^DD+N^UD+N^DU,
        // min eigenvalue ≈ -2.15e-4 on a variance scale ≈ 3.5e-4) and PSD under the
        // PSD-proven Gerber Statistic 2 denominator (T - n^NN, n^NN = both-quiet count;
        // min eigenvalue ≈ +1.47e-4). The single dense T=200 factor panel in
        // GerberCovariance_5Asset_IsPositiveSemiDefinite lands PSD by luck and does NOT
        // discriminate the two denominators; this panel does. Documented counterexample
        // from finding F-VCL-1 (COV-GERBER-PSD-DENOM-007).
        var returns = new[]
        {
            new[] { -0.0165m, 0.0049m, -0.0013m, -0.0019m, 0.0065m, 0.0133m, 0.0038m, 0.0036m, -0.0227m, 0.0116m, -0.0065m, 0.0198m, 0.0263m, -0.0327m, 0.0266m, -0.0136m, -0.0208m, -0.0046m, 0.0060m, 0.0202m, 0.0564m, -0.0293m, -0.0073m, 0.0075m, -0.0106m, -0.0344m },
            new[] { -0.0046m, -0.0005m, 0.0079m, -0.0078m, 0.0270m, 0.0044m, 0.0159m, -0.0206m, -0.0510m, 0.0156m, 0.0072m, 0.0140m, 0.0031m, -0.0060m, -0.0047m, 0.0206m, -0.0148m, -0.0054m, -0.0003m, 0.0069m, 0.0443m, -0.0102m, 0.0508m, 0.0035m, 0.0007m, -0.0084m },
            new[] { 0.0187m, 0.0340m, -0.0174m, -0.0016m, -0.0046m, -0.0105m, -0.0194m, 0.0092m, 0.0156m, 0.0066m, -0.0096m, 0.0053m, 0.0203m, 0.0099m, 0.0206m, -0.0029m, 0.0172m, 0.0142m, -0.0106m, -0.0017m, -0.0126m, 0.0039m, -0.0021m, 0.0022m, 0.0005m, -0.0106m },
            new[] { 0.0109m, -0.0215m, 0.0008m, -0.0452m, -0.0196m, 0.0145m, -0.0044m, 0.0060m, -0.0185m, -0.0299m, -0.0212m, 0.0000m, -0.0080m, 0.0351m, 0.0377m, 0.0173m, -0.0163m, 0.0027m, -0.0090m, -0.0106m, -0.0064m, 0.0005m, -0.0054m, -0.0010m, -0.0366m, -0.0108m },
            new[] { 0.0133m, 0.0108m, -0.0260m, 0.0049m, 0.0008m, -0.0251m, 0.0023m, 0.0088m, -0.0011m, 0.0343m, -0.0120m, 0.0043m, 0.0090m, 0.0091m, -0.0097m, 0.0098m, 0.0059m, 0.0460m, 0.0261m, -0.0006m, 0.0269m, -0.0189m, 0.0050m, 0.0320m, -0.0372m, 0.0210m },
        };

        var estimator = new GerberCovarianceEstimator();
        var cov = estimator.Estimate(returns);

        var minEig = MinEigenvalueSymmetric(cov);

        Assert.True(minEig >= -PrecisionStatistical,
            $"Gerber-2 estimate must be PSD (Prop. 2) on the indefinite-under-both-large panel; " +
            $"min eigenvalue = {minEig}");
    }

    [Fact]
    public void GerberCovariance_RescaleIdentity_DiagonalEqualsVariance()
    {
        // g2_ii == 1 ⇒ Sigma_ii = g2_ii * sigma_i * sigma_i = sigma_i^2.
        using var doc = LoadVector("gerber_covariance_5asset");
        var returns = GetJaggedArray(doc.RootElement.GetProperty("inputs").GetProperty("returns"));
        var sampleStd = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("sample_std"));

        var estimator = new GerberCovarianceEstimator();
        var cov = estimator.Estimate(returns);

        for (var i = 0; i < returns.Length; i++)
        {
            var expectedVar = sampleStd[i] * sampleStd[i];
            AssertWithinTolerance(cov[i, i], expectedVar, PrecisionNumeric, $"Sigma_[{i},{i}] = sigma_{i}^2: ");
        }
    }

    [Fact]
    public void GerberCovariance_IsSymmetric()
    {
        using var doc = LoadVector("gerber_covariance_5asset");
        var returns = GetJaggedArray(doc.RootElement.GetProperty("inputs").GetProperty("returns"));

        var estimator = new GerberCovarianceEstimator();
        var cov = estimator.Estimate(returns);
        var n = cov.GetLength(0);

        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                AssertWithinTolerance(cov[i, j], cov[j, i], PrecisionExact, $"Symmetry [{i},{j}]: ");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-4: Degenerate-input guards
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GerberCovariance_SingleObservation_Throws()
    {
        // T < 2 → caught by SampleCovarianceEstimator.ValidateReturns().
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m } };

        var estimator = new GerberCovarianceEstimator();
        var act = () => estimator.Estimate(returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GerberCovariance_MismatchedLengths_Throws()
    {
        var returns = new[] { new[] { 0.01m, 0.02m, 0.03m }, new[] { 0.02m, 0.01m } };

        var estimator = new GerberCovarianceEstimator();
        var act = () => estimator.Estimate(returns);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GerberCovariance_ZeroVarianceAsset_ThrowsCalculationException()
    {
        // A constant series has sigma = 0 → threshold H = 0 and an undefined Gerber
        // statistic; the estimate is degenerate. Surface, don't silently propagate.
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m },
            new[] { 0.05m, 0.05m, 0.05m, 0.05m }, // constant → sigma = 0
        };

        var estimator = new GerberCovarianceEstimator();
        var act = () => estimator.Estimate(returns);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void GerberCovariance_NullReturns_Throws()
    {
        var estimator = new GerberCovarianceEstimator();
        var act = () => estimator.Estimate(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GerberCovariance_LargeC_DiagonalEqualsVariance()
    {
        // The diagonal contract Sigma_ii = sigma_i^2 must hold for ALL valid c, because
        // an asset is perfectly concordant with itself (g2_ii = 1 by definition).
        // With a large threshold multiplier (c = 3.0) on a low-dispersion panel, EVERY
        // observation can fall within +/- c*sigma, so jointlyLarge_ii = 0. The diagonal
        // must still equal sigma_i^2 — it must not depend on the jointly-large count.
        // (Reachability boundary established for COV-GERBER-DIAGZERO-004: c > 1/sqrt(2).)
        var returns = new[]
        {
            new[] { 0.01m, 0.02m },
            new[] { 0.005m, 0.015m },
        };

        var estimator = new GerberCovarianceEstimator(c: 3.0m);
        var cov = estimator.Estimate(returns);

        for (var i = 0; i < returns.Length; i++)
        {
            // Sample std (N-1) of a 2-observation series [a, b] is |b - a| / sqrt(2);
            // variance is therefore ((b - a)^2) / 2.
            var diff = returns[i][1] - returns[i][0];
            var expectedVar = diff * diff / 2m;
            AssertWithinTolerance(cov[i, i], expectedVar, PrecisionNumeric, $"Sigma_[{i},{i}] = sigma_{i}^2 under large c: ");
        }
    }

    // ─── eigenvalue helper (symmetric Jacobi, double precision) ─────────────

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

        // Jacobi eigenvalue iteration for a symmetric matrix.
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
                    var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                    if (theta == 0.0)
                    {
                        t = 1.0;
                    }
                    var cos = 1.0 / Math.Sqrt(t * t + 1.0);
                    var sin = t * cos;
                    for (var i = 0; i < n; i++)
                    {
                        var aip = a[i, p];
                        var aiq = a[i, q];
                        a[i, p] = cos * aip - sin * aiq;
                        a[i, q] = sin * aip + cos * aiq;
                    }
                    for (var i = 0; i < n; i++)
                    {
                        var api = a[p, i];
                        var aqi = a[q, i];
                        a[p, i] = cos * api - sin * aqi;
                        a[q, i] = sin * api + cos * aqi;
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
