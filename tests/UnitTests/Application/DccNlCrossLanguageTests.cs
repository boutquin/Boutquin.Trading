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
/// Cross-language verification for the DCC-NL dynamic covariance estimator.
///
/// For V1 (per the spec) this is a <i>self-consistency</i> vector: the JSON carries a
/// deterministic N=2, T=50 synthetic return panel plus the DCC parameters (α, β). The
/// C# estimator fits the panel and the test asserts every per-date conditional
/// covariance H_t is positive semi-definite — the defining contract
/// <c>α+β&lt;1 ⇒ H_t PSD</c>.
///
/// Vector: tests/Verification/vectors/dynamic_covariance_dcc_nl.json
/// (from generate_dynamic_covariance_vectors.py).
/// </summary>
public sealed class DccNlCrossLanguageTests : CrossLanguageVerificationBase
{
    private static decimal[][] GetReturns(JsonElement element)
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

    [Fact]
    public void DccNl_SelfConsistency_AllConditionalCovariancesArePsd()
    {
        using var doc = LoadVector("dynamic_covariance_dcc_nl");
        var root = doc.RootElement;
        var alpha = (decimal)root.GetProperty("alpha").GetDouble();
        var beta = (decimal)root.GetProperty("beta").GetDouble();
        var tolerance = root.GetProperty("psd_eigenvalue_tolerance").GetDouble();
        var returns = GetReturns(root.GetProperty("returns"));

        // Fixed-parameter (V1) path: the vector's (α, β) are the recursion's actual parameters,
        // so the PSD self-consistency check tests α+β<1 ⇒ H_t PSD for *those* values rather than
        // QMLE-estimated ones.
        IDynamicCovarianceEstimator estimator =
            new DccNlCovarianceEstimator(alpha, beta, estimateParameters: false);
        estimator.Fit(returns);

        var covariances = estimator.GetConditionalCovariances();
        covariances.Should().NotBeEmpty();

        foreach (var h in covariances)
        {
            SmallestEigenvalue(h).Should().BeGreaterThanOrEqualTo(tolerance);
        }

        // One-step-ahead forecast is also PSD.
        SmallestEigenvalue(estimator.Forecast()).Should().BeGreaterThanOrEqualTo(tolerance);
    }
}
