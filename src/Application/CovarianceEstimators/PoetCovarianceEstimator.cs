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
/// POET (Principal Orthogonal complEment Thresholding) covariance estimator.
/// Decomposes the sample covariance into a low-rank factor component (top K principal
/// components) plus a sparse residual obtained by soft-thresholding off-diagonal entries.
/// Well-suited for large equity universes with strong factor structure. Ideal for use
/// with <c>HierarchicalRiskParityConstruction</c> and <c>PrincipalComponentRiskParityConstruction</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>PSD caution:</strong> soft-thresholding of the residual covariance can break
/// positive semi-definiteness when the threshold multiplier is aggressive. Apply
/// <c>NearestPsdProjection</c> if PSD is required by the downstream optimizer.
/// </para>
/// Reference: Fan, J., Liao, Y. &amp; Mincheva, M. (2013). "Large covariance estimation
/// by thresholding principal orthogonal complements." Journal of the Royal Statistical
/// Society, Series B, 75(4), 603-680.
/// </remarks>
public sealed class PoetCovarianceEstimator : ICovarianceEstimator
{
    private readonly NumericsStats.PoetCovarianceEstimator _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="PoetCovarianceEstimator"/> class.
    /// </summary>
    /// <param name="numFactors">
    /// Number of latent factors (principal components) to retain. Default 1.
    /// Increase for multi-factor universes (e.g., 3 for Fama-French).
    /// </param>
    /// <param name="thresholdMultiplier">
    /// Soft-threshold multiplier for the residual covariance. Default 0.5.
    /// Higher values produce sparser residuals; values above ~2.0 risk breaking PSD.
    /// </param>
    public PoetCovarianceEstimator(int numFactors = 1, double thresholdMultiplier = 0.5)
    {
        _inner = new NumericsStats.PoetCovarianceEstimator(numFactors, thresholdMultiplier);
    }

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return _inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
