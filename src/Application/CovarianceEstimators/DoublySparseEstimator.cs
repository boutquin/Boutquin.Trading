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
/// Doubly-sparse covariance estimator (DSCE). Decomposes the sample covariance into a
/// signal component with hard-thresholded (sparsified) eigenvectors and a noise component
/// with eigenvalues replaced by their average. Uses the Marcenko-Pastur upper bound to
/// partition signal from noise eigenvalues.
/// </summary>
/// <remarks>
/// Reference: "Doubly Sparse Estimation of High-Dimensional Covariance Matrices."
/// Econometrics and Statistics (2024).
/// </remarks>
public sealed class DoublySparseEstimator : ICovarianceEstimator
{
    private readonly NumericsStats.DoublySparseEstimator _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoublySparseEstimator"/> class.
    /// </summary>
    /// <param name="eigenvectorThreshold">
    /// Hard-threshold applied to signal eigenvector entries. Entries with absolute
    /// value below this threshold are zeroed and the eigenvector is re-normalized.
    /// Default 0.1. Increase for sparser (more interpretable) factor structure.
    /// </param>
    public DoublySparseEstimator(decimal eigenvectorThreshold = 0.1m)
    {
        _inner = new NumericsStats.DoublySparseEstimator(eigenvectorThreshold);
    }

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return _inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
