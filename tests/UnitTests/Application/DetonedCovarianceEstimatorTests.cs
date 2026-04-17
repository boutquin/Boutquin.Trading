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
/// Tests for <see cref="DetonedCovarianceEstimator"/>.
/// </summary>
public sealed class DetonedCovarianceEstimatorTests
{
    private const decimal Precision = 1e-10m;

    /// <summary>
    /// Detoned estimator should produce a valid symmetric matrix with positive diagonal.
    /// </summary>
    [Fact]
    public void Estimate_ThreeAssets_ProducesValidCovarianceMatrix()
    {
        var returns = new[]
        {
            new[] { 2m, -1m, 3m, -2m, 1m, 4m, -3m, 2m, -1m, 3m },
            new[] { 1.9m, -1.1m, 3.1m, -1.9m, 0.9m, 4.1m, -2.9m, 2.1m, -0.9m, 2.9m },
            new[] { 1m, 2m, -1m, -2m, 3m, -3m, 1m, -1m, 2m, -2m }
        };

        var estimator = new DetonedCovarianceEstimator();
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
                Math.Abs(cov[i, j] - cov[j, i]).Should().BeLessThan(Precision,
                    $"Covariance matrix should be symmetric at [{i},{j}]");
            }
        }
    }

    /// <summary>
    /// Detoning should shrink the largest eigenvalue, reducing the total variance
    /// relative to the denoised-only estimator.
    /// </summary>
    [Fact]
    public void Estimate_ThreeAssets_ReducesLargestEigenvalueContribution()
    {
        var returns = new[]
        {
            new[] { 2m, -1m, 3m, -2m, 1m, 4m, -3m, 2m, -1m, 3m },
            new[] { 1.9m, -1.1m, 3.1m, -1.9m, 0.9m, 4.1m, -2.9m, 2.1m, -0.9m, 2.9m },
            new[] { 1m, 2m, -1m, -2m, 3m, -3m, 1m, -1m, 2m, -2m }
        };

        var denoised = new DenoisedCovarianceEstimator();
        var detoned = new DetonedCovarianceEstimator();

        var denoisedCov = denoised.Estimate(returns);
        var detonedCov = detoned.Estimate(returns);

        // The off-diagonal magnitude should generally be reduced after detoning,
        // since PC1 inflates all pairwise covariances.
        var denoisedOffDiagSum = 0m;
        var detonedOffDiagSum = 0m;
        for (var i = 0; i < 3; i++)
        {
            for (var j = i + 1; j < 3; j++)
            {
                denoisedOffDiagSum += Math.Abs(denoisedCov[i, j]);
                detonedOffDiagSum += Math.Abs(detonedCov[i, j]);
            }
        }

        detonedOffDiagSum.Should().BeLessThan(denoisedOffDiagSum,
            "Detoning should reduce off-diagonal covariance magnitude by shrinking PC1");
    }

    /// <summary>
    /// With an identity correlation matrix (uncorrelated assets), detoning has minimal effect
    /// because all eigenvalues are equal — there is no dominant PC1 to shrink.
    /// </summary>
    [Fact]
    public void Estimate_UncorrelatedAssets_MinimalDetoningEffect()
    {
        // Construct uncorrelated return series
        var returns = new[]
        {
            new[] { 1m, -1m, 2m, -2m, 1m, -1m, 2m, -2m, 1m, -1m },
            new[] { 1m, 1m, -1m, -1m, 2m, -2m, 1m, 1m, -1m, -1m },
            new[] { -1m, 2m, 1m, -2m, -1m, 1m, 2m, -1m, -2m, 1m }
        };

        var denoised = new DenoisedCovarianceEstimator();
        var detoned = new DetonedCovarianceEstimator();

        var denoisedCov = denoised.Estimate(returns);
        var detonedCov = detoned.Estimate(returns);

        // Variances should be very similar since no dominant eigenvalue to shrink
        for (var i = 0; i < 3; i++)
        {
            var ratio = detonedCov[i, i] / denoisedCov[i, i];
            ratio.Should().BeGreaterThan(0.5m, "Detoning should not destroy variance for uncorrelated assets");
        }
    }

    /// <summary>
    /// For N &lt; 3, detoning delegates to sample covariance (same as denoiser).
    /// </summary>
    [Fact]
    public void Estimate_TwoAssets_DelegatesToSample()
    {
        var returns = new[]
        {
            new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m },
            new[] { 0.02m, 0.04m, -0.02m, 0.06m, -0.04m }
        };

        var detoned = new DetonedCovarianceEstimator();
        var sample = new SampleCovarianceEstimator();

        var detonedCov = detoned.Estimate(returns);
        var sampleCov = sample.Estimate(returns);

        for (var i = 0; i < 2; i++)
        {
            for (var j = 0; j < 2; j++)
            {
                detonedCov[i, j].Should().BeApproximately(sampleCov[i, j], 1e-12m,
                    $"Detoned[{i},{j}] should match sample for N<3");
            }
        }
    }

    /// <summary>
    /// Configurable detoning alpha controls how much PC1 is shrunk.
    /// Alpha=0 should equal denoised (no detoning); alpha=1 should shrink PC1 maximally.
    /// </summary>
    [Fact]
    public void Estimate_AlphaZero_EqualsDenoised()
    {
        var returns = new[]
        {
            new[] { 2m, -1m, 3m, -2m, 1m, 4m, -3m, 2m, -1m, 3m },
            new[] { 1.9m, -1.1m, 3.1m, -1.9m, 0.9m, 4.1m, -2.9m, 2.1m, -0.9m, 2.9m },
            new[] { 1m, 2m, -1m, -2m, 3m, -3m, 1m, -1m, 2m, -2m }
        };

        var denoised = new DenoisedCovarianceEstimator();
        var detonedNoEffect = new DetonedCovarianceEstimator(detoningAlpha: 0m);

        var denoisedCov = denoised.Estimate(returns);
        var detonedCov = detonedNoEffect.Estimate(returns);

        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                detonedCov[i, j].Should().BeApproximately(denoisedCov[i, j], 1e-10m,
                    $"Alpha=0 should produce identical result to denoised at [{i},{j}]");
            }
        }
    }

    /// <summary>
    /// Higher alpha should produce more off-diagonal shrinkage.
    /// </summary>
    [Fact]
    public void Estimate_HigherAlpha_MoreShrinkage()
    {
        var returns = new[]
        {
            new[] { 2m, -1m, 3m, -2m, 1m, 4m, -3m, 2m, -1m, 3m },
            new[] { 1.9m, -1.1m, 3.1m, -1.9m, 0.9m, 4.1m, -2.9m, 2.1m, -0.9m, 2.9m },
            new[] { 1m, 2m, -1m, -2m, 3m, -3m, 1m, -1m, 2m, -2m }
        };

        var lowAlpha = new DetonedCovarianceEstimator(detoningAlpha: 0.3m);
        var highAlpha = new DetonedCovarianceEstimator(detoningAlpha: 0.9m);

        var lowCov = lowAlpha.Estimate(returns);
        var highCov = highAlpha.Estimate(returns);

        var lowOffDiag = 0m;
        var highOffDiag = 0m;
        for (var i = 0; i < 3; i++)
        {
            for (var j = i + 1; j < 3; j++)
            {
                lowOffDiag += Math.Abs(lowCov[i, j]);
                highOffDiag += Math.Abs(highCov[i, j]);
            }
        }

        highOffDiag.Should().BeLessThanOrEqualTo(lowOffDiag,
            "Higher detoning alpha should produce equal or more off-diagonal shrinkage");
    }

    [Fact]
    public void Estimate_NullReturns_ThrowsArgumentException()
    {
        var estimator = new DetonedCovarianceEstimator();

        var act = () => estimator.Estimate(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Estimate_EmptyReturns_ThrowsArgumentException()
    {
        var estimator = new DetonedCovarianceEstimator();

        var act = () => estimator.Estimate([]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeAlpha_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new DetonedCovarianceEstimator(detoningAlpha: -0.1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_AlphaGreaterThanOne_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new DetonedCovarianceEstimator(detoningAlpha: 1.1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
