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
/// Closed-form Oracle Approximating Shrinkage (OAS) covariance estimator.
/// Approximates the oracle shrinkage intensity under the Gaussian assumption,
/// producing better-conditioned estimates than Ledoit-Wolf (2004) scaled-identity
/// shrinkage when T/N is large.
/// </summary>
/// <remarks>
/// Reference: Chen, Y., Wiesel, A., Eldar, Y. C., &amp; Hero, A. O. (2010).
/// "Shrinkage Algorithms for MMSE Covariance Estimation." IEEE Transactions
/// on Signal Processing, 58(10), 5016-5029.
/// </remarks>
public sealed class OracleApproximatingShrinkageEstimator : ICovarianceEstimator
{
    private static readonly NumericsStats.OracleApproximatingShrinkageEstimator s_inner = new();

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return s_inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
