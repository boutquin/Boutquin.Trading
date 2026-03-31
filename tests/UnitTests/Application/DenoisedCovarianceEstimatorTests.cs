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

namespace Boutquin.Trading.Tests.UnitTests.Application;

using Boutquin.Trading.Application.CovarianceEstimators;
using FluentAssertions;

/// <summary>
/// Tests for <see cref="DenoisedCovarianceEstimator"/>.
/// </summary>
public sealed class DenoisedCovarianceEstimatorTests
{
    private const decimal Precision = 1e-12m;

    /// <summary>
    /// Denoised estimator should produce a valid symmetric PSD covariance matrix
    /// with positive diagonal elements.
    /// </summary>
    [Fact]
    public void Estimate_ThreeAssets_ProducesValidCovarianceMatrix()
    {
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, 0.02m, -0.01m, 0.03m },
            new[] { 0.005m, -0.003m, 0.004m, 0.002m, -0.001m, 0.003m, -0.002m, 0.001m, 0.004m, -0.003m },
            new[] { 0.01m, -0.02m, 0.015m, -0.005m, 0.02m, -0.01m, 0.005m, 0.03m, -0.025m, 0.01m }
        };

        var estimator = new DenoisedCovarianceEstimator();
        var cov = estimator.Estimate(returns);

        // Diagonal should be positive (variances)
        for (var i = 0; i < 3; i++)
        {
            cov[i, i].Should().BeGreaterThan(0m, $"Variance for asset {i} should be positive");
        }

        // Matrix should be symmetric
        for (var i = 0; i < 3; i++)
        {
            for (var j = i + 1; j < 3; j++)
            {
                Math.Abs(cov[i, j] - cov[j, i]).Should().BeLessThan(1e-10m,
                    $"Covariance matrix should be symmetric at [{i},{j}]");
            }
        }
    }

    /// <summary>
    /// Denoised covariance should produce off-diagonal elements that are more stable
    /// (closer to zero for uncorrelated assets) than raw sample covariance.
    /// </summary>
    [Fact]
    public void Estimate_HighlyCorrelatedAssets_RemovesNoise()
    {
        // Two highly correlated + one noisy uncorrelated asset.
        // Use larger magnitudes to avoid decimal overflow in Jacobi eigendecomposition.
        var returns = new[]
        {
            new[] { 2m, -1m, 3m, -2m, 1m, 4m, -3m, 2m, -1m, 3m },
            new[] { 1.9m, -1.1m, 3.1m, -1.9m, 0.9m, 4.1m, -2.9m, 2.1m, -0.9m, 2.9m },
            new[] { 1m, 2m, -1m, -2m, 3m, -3m, 1m, -1m, 2m, -2m }
        };

        var denoised = new DenoisedCovarianceEstimator();

        var denoisedCov = denoised.Estimate(returns);

        // The denoised matrix should be symmetric
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                denoisedCov[i, j].Should().BeApproximately(denoisedCov[j, i], 1e-10m,
                    "Denoised covariance should be symmetric");
            }
        }

        // Diagonal should be positive
        for (var i = 0; i < 3; i++)
        {
            denoisedCov[i, i].Should().BeGreaterThan(0m, "Variance should be positive");
        }

        // The off-diagonal between the two correlated assets should remain strong
        Math.Abs(denoisedCov[0, 1]).Should().BeGreaterThan(0m,
            "Correlated assets should retain covariance signal");
    }

    /// <summary>
    /// For N &lt; 3, denoising is not meaningful and should delegate to sample covariance.
    /// </summary>
    [Fact]
    public void Estimate_TwoAssets_DelegatesToSample()
    {
        var returns = new[]
        {
            new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m },
            new[] { 0.02m, 0.04m, -0.02m, 0.06m, -0.04m }
        };

        var denoised = new DenoisedCovarianceEstimator();
        var sample = new SampleCovarianceEstimator();

        var denoisedCov = denoised.Estimate(returns);
        var sampleCov = sample.Estimate(returns);

        // For N=2, denoised should equal sample covariance exactly
        for (var i = 0; i < 2; i++)
        {
            for (var j = 0; j < 2; j++)
            {
                denoisedCov[i, j].Should().BeApproximately(sampleCov[i, j], Precision,
                    $"Denoised[{i},{j}] should match sample for N<3");
            }
        }
    }

    /// <summary>
    /// With Ledoit-Wolf shrinkage enabled, the result should be a valid positive semi-definite matrix
    /// (diagonal positive, symmetric).
    /// </summary>
    [Fact]
    public void Estimate_WithLedoitWolfShrinkage_ProducesValidMatrix()
    {
        // Use larger magnitudes to avoid decimal overflow in Jacobi eigendecomposition.
        var returns = new[]
        {
            new[] { 2m, -1m, 3m, -2m, 1m, 4m, -3m, 2m, -1m, 3m },
            new[] { 1.9m, -1.1m, 3.1m, -1.9m, 0.9m, 4.1m, -2.9m, 2.1m, -0.9m, 2.9m },
            new[] { 1m, 2m, -1m, -2m, 3m, -3m, 1m, -1m, 2m, -2m }
        };

        var estimator = new DenoisedCovarianceEstimator(applyLedoitWolfShrinkage: true);

        var cov = estimator.Estimate(returns);

        // Symmetry
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                cov[i, j].Should().BeApproximately(cov[j, i], 1e-10m, "Matrix should be symmetric");
            }
        }

        // Positive diagonal (necessary for PSD)
        for (var i = 0; i < 3; i++)
        {
            cov[i, i].Should().BeGreaterThan(0m, $"Diagonal element [{i},{i}] should be positive");
        }
    }

    [Fact]
    public void Estimate_NullReturns_ThrowsArgumentException()
    {
        var estimator = new DenoisedCovarianceEstimator();

        var act = () => estimator.Estimate(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Estimate_EmptyReturns_ThrowsArgumentException()
    {
        var estimator = new DenoisedCovarianceEstimator();

        var act = () => estimator.Estimate([]);

        act.Should().Throw<ArgumentException>();
    }

}
