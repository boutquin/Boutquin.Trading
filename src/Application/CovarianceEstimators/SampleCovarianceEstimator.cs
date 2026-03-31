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

namespace Boutquin.Trading.Application.CovarianceEstimators;

/// <summary>
/// Computes the sample covariance matrix using the unbiased estimator (N-1 divisor).
/// </summary>
public sealed class SampleCovarianceEstimator : ICovarianceEstimator
{
    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        ValidateReturns(returns);

        var n = returns.Length;
        var t = returns[0].Length;
        var means = new decimal[n];

        for (var i = 0; i < n; i++)
        {
            means[i] = returns[i].Average();
        }

        var cov = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                var sum = 0m;
                for (var k = 0; k < t; k++)
                {
                    sum += (returns[i][k] - means[i]) * (returns[j][k] - means[j]);
                }

                cov[i, j] = sum / (t - 1);
                cov[j, i] = cov[i, j];
            }
        }

        return cov;
    }

    internal static void ValidateReturns(decimal[][] returns)
    {
        if (returns is null || returns.Length == 0)
        {
            throw new ArgumentException("Returns array must not be null or empty.", nameof(returns));
        }

        var t = returns[0].Length;
        if (t < 2)
        {
            throw new ArgumentException("Each return series must contain at least two observations.", nameof(returns));
        }

        for (var i = 1; i < returns.Length; i++)
        {
            if (returns[i].Length != t)
            {
                throw new ArgumentException("All return series must have the same length.", nameof(returns));
            }
        }
    }
}
