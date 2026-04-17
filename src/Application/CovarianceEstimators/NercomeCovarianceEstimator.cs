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
/// NERCOME (Nonparametric Eigenvalue-Regularized COvariance Matrix Estimator).
/// Splits the observation window into two disjoint halves: eigenvectors are estimated
/// on the first half, eigenvalues are estimated on the second half. Fully nonparametric
/// — makes no distributional assumptions, making it suitable for fat-tailed return series.
/// Requires at least 4 observations (2 per half).
/// </summary>
/// <remarks>
/// Reference: Abadir, K. M., Distaso, W. &amp; Zikes, F. (2014). "Design-free
/// estimation of variance matrices." Journal of Econometrics, 181(2), 165-180.
/// </remarks>
public sealed class NercomeCovarianceEstimator : ICovarianceEstimator
{
    private readonly NumericsStats.NercomeCovarianceEstimator _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="NercomeCovarianceEstimator"/> class.
    /// </summary>
    /// <param name="splitFraction">
    /// Fraction of observations assigned to the first half for eigenvector estimation.
    /// Default 0.5 (equal split). Must be in (0, 1).
    /// </param>
    public NercomeCovarianceEstimator(decimal splitFraction = 0.5m)
    {
        _inner = new NumericsStats.NercomeCovarianceEstimator(splitFraction);
    }

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return _inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
