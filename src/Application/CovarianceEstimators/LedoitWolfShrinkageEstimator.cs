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
    private static readonly NumericsStats.LedoitWolfShrinkageEstimator s_inner = new();

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return s_inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
