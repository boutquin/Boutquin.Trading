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
/// Implements the Ledoit-Wolf shrinkage estimator for covariance matrices.
/// Shrinks the sample covariance matrix toward a structured target (scaled identity matrix)
/// using an analytically optimal shrinkage intensity.
/// </summary>
/// <remarks>
/// Reference: Ledoit, O. &amp; Wolf, M. (2004). "A well-conditioned estimator for
/// large-dimensional covariance matrices." Journal of Multivariate Analysis, 88(2), 365-411.
/// </remarks>
public sealed class LedoitWolfShrinkageEstimator : ICovarianceEstimator
{
    private static readonly SampleCovarianceEstimator s_sharedEstimator = new();

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);

        var n = returns.Length; // Number of assets
        var t = returns[0].Length; // Number of observations

        // Step 1: Compute sample covariance matrix
        var sampleCov = s_sharedEstimator.Estimate(returns);

        // Step 2: Compute the shrinkage target — scaled identity matrix
        // Target = mu * I, where mu = average of diagonal elements
        var mu = 0m;
        for (var i = 0; i < n; i++)
        {
            mu += sampleCov[i, i];
        }

        mu /= n;

        // Step 3: Compute optimal shrinkage intensity (delta)
        // Using the Ledoit-Wolf analytical formula
        var means = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            means[i] = returns[i].Average();
        }

        // Compute sum of squared Frobenius norms of (S - F)
        var sumSquaredDiff = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var target = i == j ? mu : 0m;
                var diff = sampleCov[i, j] - target;
                sumSquaredDiff += diff * diff;
            }
        }

        // Compute the sum of asymptotic variances of sample covariance entries
        var piSum = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var sum = 0m;
                for (var k = 0; k < t; k++)
                {
                    var x = (returns[i][k] - means[i]) * (returns[j][k] - means[j]) - sampleCov[i, j];
                    sum += x * x;
                }

                piSum += sum / t;
            }
        }

        // Compute rho: sum of asymptotic covariances of sample entries with target entries.
        // For scaled identity target F = mu*I, off-diagonal target entries are 0,
        // so rho_ij = 0 for i != j. Diagonal rho_ii == pi_ii.
        // Therefore: rhoSum = sum of diagonal pi terms only.
        // This makes (piSum - rhoSum) = sum of OFF-DIAGONAL pi terms.
        // Reference: Ledoit & Wolf (2004), Equation (2) for target = mu*I.
        var rhoSum = 0m;
        for (var i = 0; i < n; i++)
        {
            var sum = 0m;
            for (var k = 0; k < t; k++)
            {
                var zki = returns[i][k] - means[i];
                var term = zki * zki - sampleCov[i, i];
                sum += term * term;
            }

            rhoSum += sum / t;
        }

        // Shrinkage intensity: delta = max(0, min(1, (piSum - rhoSum) / (T * gamma)))
        // Clamp to [0, 1]
        var delta = sumSquaredDiff == 0m ? 1m : (piSum - rhoSum) / (t * sumSquaredDiff);
        delta = Math.Max(0m, Math.Min(1m, delta));

        // Step 4: Compute shrunk covariance matrix
        // S_shrunk = delta * F + (1 - delta) * S
        var shrunk = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var target = i == j ? mu : 0m;
                shrunk[i, j] = delta * target + (1m - delta) * sampleCov[i, j];
            }
        }

        return shrunk;
    }
}
