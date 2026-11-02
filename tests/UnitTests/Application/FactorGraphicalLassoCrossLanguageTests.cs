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
/// Cross-language verification tests for <see cref="FactorGraphicalLassoEstimator"/>.
///
/// The vector (tests/Verification/vectors/covariance_factor_graphical_lasso.json, from
/// generate_factor_graphical_lasso_vectors.py) independently computes PCA with NumPy/LAPACK and
/// weighted residual graphical lasso by scaling scikit-learn's correlation-space solution.
/// Its direct residual-plus-factor covariance reconstruction checks the C# SMW route. The panel
/// distinguishes the corrected sample-size-weighted BIC from the old Numerics package: the
/// selected penalty changes from 1.0 to 0.001. PSD and symmetry are also verified directly.
///
/// Reference: Lee &amp; Seregina (2020/2023), "Optimal Portfolio Using Factor Graphical Lasso",
/// arXiv 2011.00435.
/// </summary>
public sealed class FactorGraphicalLassoCrossLanguageTests : CrossLanguageVerificationBase
{
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

    [Fact]
    public void FactorGraphicalLasso_3Asset_MatchesIndependentCovariance()
    {
        using var doc = LoadVector("covariance_factor_graphical_lasso");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var k = inputs.GetProperty("num_factors").GetInt32();
        var expected = GetMatrix(doc.RootElement.GetProperty("expected").GetProperty("covariance"));

        var estimator = new FactorGraphicalLassoEstimator(numFactors: k);
        var result = estimator.Estimate(returns);

        Assert.Equal(expected.GetLength(0), result.GetLength(0));
        Assert.Equal(expected.GetLength(1), result.GetLength(1));
        for (var i = 0; i < expected.GetLength(0); i++)
        {
            for (var j = 0; j < expected.GetLength(1); j++)
            {
                AssertWithinTolerance(result[i, j], expected[i, j], PrecisionNumeric,
                    $"FGL[{i},{j}]: ");
            }
        }
    }

    [Fact]
    public void FactorGraphicalLasso_3Asset_IsPositiveSemiDefinite()
    {
        // Solver-independent fidelity ground truth: the returned covariance is PSD
        // (eq. 3.5 + GL positive-definiteness). The vector pins is_psd = true.
        using var doc = LoadVector("covariance_factor_graphical_lasso");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var k = inputs.GetProperty("num_factors").GetInt32();

        Assert.True(doc.RootElement.GetProperty("expected").GetProperty("is_psd").GetBoolean());

        var estimator = new FactorGraphicalLassoEstimator(numFactors: k);
        var cov = estimator.Estimate(returns);

        var minEig = MinEigenvalueSymmetric(cov);
        Assert.True(minEig >= -PrecisionStatistical,
            $"Factor Graphical Lasso estimate must be PSD; min eigenvalue = {minEig}");
    }

    [Fact]
    public void FactorGraphicalLasso_3Asset_IsSymmetric()
    {
        using var doc = LoadVector("covariance_factor_graphical_lasso");
        var inputs = doc.RootElement.GetProperty("inputs");
        var returns = GetJaggedArray(inputs.GetProperty("returns"));
        var k = inputs.GetProperty("num_factors").GetInt32();

        var estimator = new FactorGraphicalLassoEstimator(numFactors: k);
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
