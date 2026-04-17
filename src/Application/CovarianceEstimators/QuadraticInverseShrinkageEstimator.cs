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
/// Quadratic-Inverse Shrinkage (QIS) covariance estimator — Ledoit-Wolf analytical
/// nonlinear shrinkage. Shrinks each sample eigenvalue individually using the
/// quadratic-inverse kernel, producing superior estimates to all linear shrinkage
/// methods for medium and large portfolios (N ≥ 10).
/// </summary>
/// <remarks>
/// Reference: Ledoit, O. &amp; Wolf, M. (2022). "Quadratic shrinkage for
/// large covariance matrices." Bernoulli, 28(3), 1519-1547.
/// </remarks>
public sealed class QuadraticInverseShrinkageEstimator : ICovarianceEstimator
{
    private static readonly NumericsStats.QuadraticInverseShrinkageEstimator s_inner = new();

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return s_inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
