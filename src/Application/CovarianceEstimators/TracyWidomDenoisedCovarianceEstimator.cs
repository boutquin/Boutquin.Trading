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

using NumericsStats = Boutquin.Numerics.Statistics;

namespace Boutquin.Trading.Application.CovarianceEstimators;

/// <summary>
/// Tracy-Widom denoised covariance estimator. Uses the Tracy-Widom distribution to
/// apply a sharper finite-sample signal/noise threshold than the asymptotic
/// Marcenko-Pastur bound used by <see cref="DenoisedCovarianceEstimator"/>, better
/// accounting for finite-sample fluctuations of the largest noise eigenvalue.
/// Preferred over <see cref="DenoisedCovarianceEstimator"/> when T/N &lt; 5.
/// </summary>
/// <remarks>
/// References:
/// Johnstone, I. M. (2001). "On the Distribution of the Largest Eigenvalue
/// in Principal Components Analysis." Annals of Statistics, 29(2), 295-327.
/// Bun, J., Bouchaud, J.-P. &amp; Potters, M. (2017). "Cleaning Large Correlation
/// Matrices: Tools from Random Matrix Theory." Physics Reports, 666, 1-109.
/// </remarks>
public sealed class TracyWidomDenoisedCovarianceEstimator : ICovarianceEstimator
{
    private static readonly NumericsStats.TracyWidomDenoisedCovarianceEstimator s_inner = new();

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return s_inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
