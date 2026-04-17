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
/// Detoned covariance estimator that extends denoising with market-factor (PC1) shrinkage.
///
/// The market factor (largest eigenvalue) inflates all pairwise covariances, making the
/// optimizer underestimate diversification potential. Detoning shrinks PC1's eigenvalue
/// toward the average signal eigenvalue, letting the optimizer see residual diversification
/// structure more clearly.
///
/// Based on Lopez de Prado (2020), "Machine Learning for Symbol Managers", Chapter 2.
///
/// The detoning alpha parameter controls shrinkage intensity:
///   alpha = 0 → no detoning (identical to denoised)
///   alpha = 1 → PC1 eigenvalue replaced with average of remaining signal eigenvalues
/// </summary>
public sealed class DetonedCovarianceEstimator : ICovarianceEstimator
{
    private readonly NumericsStats.DetonedCovarianceEstimator _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetonedCovarianceEstimator"/> class.
    /// </summary>
    /// <param name="detoningAlpha">
    /// Controls how much to shrink PC1's eigenvalue toward the mean of remaining signal eigenvalues.
    /// Must be in [0, 1]. Default is 1.0 (full detoning).
    /// </param>
    public DetonedCovarianceEstimator(decimal detoningAlpha = 1.0m)
    {
        _inner = new NumericsStats.DetonedCovarianceEstimator(detoningAlpha);
    }

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return _inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
