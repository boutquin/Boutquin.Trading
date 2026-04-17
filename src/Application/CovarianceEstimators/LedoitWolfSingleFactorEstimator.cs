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
/// Ledoit-Wolf shrinkage toward a single-factor (market) target. The target covariance
/// is induced by a one-factor model where each asset loads on an equally-weighted
/// market factor. Well-suited for pure equity portfolios where market beta dominates.
/// </summary>
/// <remarks>
/// Reference: Ledoit, O. &amp; Wolf, M. (2003). "Improved Estimation of the Covariance
/// Matrix of Stock Returns with an Application to Portfolio Selection."
/// Journal of Empirical Finance, 10(5), 603-621.
/// </remarks>
public sealed class LedoitWolfSingleFactorEstimator : ICovarianceEstimator
{
    private static readonly NumericsStats.LedoitWolfSingleFactorEstimator s_inner = new();

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return s_inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
