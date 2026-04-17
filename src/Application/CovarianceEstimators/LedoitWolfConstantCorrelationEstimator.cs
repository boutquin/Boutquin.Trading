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
/// Ledoit-Wolf shrinkage toward the constant-correlation target. The target matrix
/// preserves each asset's own variance on the diagonal and uses the average pairwise
/// sample correlation on every off-diagonal entry — a more realistic target for equity
/// portfolios than the scaled-identity used by <see cref="LedoitWolfShrinkageEstimator"/>.
/// </summary>
/// <remarks>
/// Reference: Ledoit, O. &amp; Wolf, M. (2004). "Honey, I Shrunk the Sample
/// Covariance Matrix." Journal of Portfolio Management, 30(4), 110-119.
/// </remarks>
public sealed class LedoitWolfConstantCorrelationEstimator : ICovarianceEstimator
{
    private static readonly NumericsStats.LedoitWolfConstantCorrelationEstimator s_inner = new();

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return s_inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
