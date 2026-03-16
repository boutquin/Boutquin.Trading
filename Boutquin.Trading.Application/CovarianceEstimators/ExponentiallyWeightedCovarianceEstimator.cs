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
/// Computes the exponentially weighted moving average (EWMA) covariance matrix.
/// Recent observations receive geometrically higher weight via the decay factor (lambda).
/// </summary>
public sealed class ExponentiallyWeightedCovarianceEstimator : ICovarianceEstimator
{
    private readonly decimal _lambda;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentiallyWeightedCovarianceEstimator"/> class.
    /// </summary>
    /// <param name="lambda">The decay factor, typically 0.94 for daily data (RiskMetrics). Must be in (0, 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lambda"/> is not in the range (0, 1).</exception>
    public ExponentiallyWeightedCovarianceEstimator(decimal lambda = 0.94m)
    {
        if (lambda <= 0m || lambda >= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(lambda), lambda, "Lambda must be between 0 and 1 exclusive.");
        }

        _lambda = lambda;
    }

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);

        var n = returns.Length;
        var t = returns[0].Length;
        var means = new decimal[n];

        for (var i = 0; i < n; i++)
        {
            means[i] = returns[i].Average();
        }

        // Compute weights: w[k] = (1-λ) * λ^(T-1-k) for k=0..T-1
        // Most recent observation (k=T-1) gets weight (1-λ), oldest gets (1-λ)*λ^(T-1)
        var weights = new decimal[t];
        var weightSum = 0m;

        for (var k = 0; k < t; k++)
        {
            weights[k] = (decimal)Math.Pow((double)_lambda, t - 1 - k);
            weightSum += weights[k];
        }

        // Normalize weights
        for (var k = 0; k < t; k++)
        {
            weights[k] /= weightSum;
        }

        var cov = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                var sum = 0m;
                for (var k = 0; k < t; k++)
                {
                    sum += weights[k] * (returns[i][k] - means[i]) * (returns[j][k] - means[j]);
                }

                cov[i, j] = sum;
                cov[j, i] = sum;
            }
        }

        return cov;
    }
}
