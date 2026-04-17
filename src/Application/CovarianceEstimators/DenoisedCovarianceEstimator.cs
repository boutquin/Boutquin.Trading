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
/// Denoised covariance estimator using Random Matrix Theory (Marcenko-Pastur distribution).
/// Identifies noise eigenvalues in the sample covariance matrix and replaces them with their
/// average, preserving total variance (trace) while removing estimation noise.
///
/// Based on Lopez de Prado (2018), "Advances in Financial Machine Learning", Chapter 2.
///
/// Optionally composes with Ledoit-Wolf shrinkage for additional regularization.
/// </summary>
public sealed class DenoisedCovarianceEstimator : ICovarianceEstimator
{
    private readonly NumericsStats.DenoisedCovarianceEstimator _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="DenoisedCovarianceEstimator"/> class.
    /// </summary>
    /// <param name="applyLedoitWolfShrinkage">
    /// When true, applies Ledoit-Wolf shrinkage after denoising for additional regularization.
    /// Default is false (pure denoising).
    /// </param>
    public DenoisedCovarianceEstimator(bool applyLedoitWolfShrinkage = false)
    {
        _inner = new NumericsStats.DenoisedCovarianceEstimator(applyLedoitWolfShrinkage);
    }

    /// <inheritdoc />
    public decimal[,] Estimate(decimal[][] returns)
    {
        SampleCovarianceEstimator.ValidateReturns(returns);
        return _inner.Estimate(new NumericsStats.ReturnsMatrix(returns).AsTimeByAsset());
    }
}
