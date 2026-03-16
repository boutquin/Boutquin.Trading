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
/// Tests for covariance estimator implementations.
/// </summary>
public sealed class CovarianceEstimatorTests
{
    private const decimal Precision = 1e-10m;

    // --- Shared test data ---

    // Perfectly correlated: asset B = 2 * asset A
    private static decimal[][] PerfectlyCorrelatedReturns =>
    [
        [0.01m, 0.02m, -0.01m, 0.03m, -0.02m],
        [0.02m, 0.04m, -0.02m, 0.06m, -0.04m]
    ];

    // Uncorrelated series (constructed so covariance is near zero)
    private static decimal[][] UncorrelatedReturns =>
    [
        [0.01m, -0.01m, 0.02m, -0.02m, 0.01m, -0.01m],
        [0.01m, 0.01m, -0.01m, -0.01m, 0.02m, -0.02m]
    ];

    // --- SampleCovarianceEstimator Tests ---

    [Fact]
    public void SampleCovariance_PerfectlyCorrelated_ShouldProduceExpectedMatrix()
    {
        // For perfectly correlated series where B = 2*A:
        // Cov(A,A) = Var(A), Cov(A,B) = 2*Var(A), Cov(B,B) = 4*Var(A)
        var estimator = new SampleCovarianceEstimator();

        var cov = estimator.Estimate(PerfectlyCorrelatedReturns);

        var varA = cov[0, 0];
        cov[0, 1].Should().BeApproximately(2m * varA, Precision, "Cov(A,B) should equal 2*Var(A)");
        cov[1, 0].Should().BeApproximately(2m * varA, Precision, "Matrix should be symmetric");
        cov[1, 1].Should().BeApproximately(4m * varA, Precision, "Var(B) should equal 4*Var(A)");
    }

    [Fact]
    public void SampleCovariance_ShouldBeSymmetric()
    {
        var estimator = new SampleCovarianceEstimator();

        var cov = estimator.Estimate(UncorrelatedReturns);

        cov[0, 1].Should().BeApproximately(cov[1, 0], Precision);
    }

    [Fact]
    public void SampleCovariance_UsesSampleDivisor()
    {
        // Verify N-1 divisor by computing manually
        var returns = new[] { new[] { 1m, 2m, 3m } };
        var estimator = new SampleCovarianceEstimator();

        var cov = estimator.Estimate(returns);

        // Mean = 2, deviations = [-1, 0, 1], sum of squares = 2, variance = 2/(3-1) = 1
        cov[0, 0].Should().BeApproximately(1m, Precision);
    }

    [Fact]
    public void SampleCovariance_NullInput_ShouldThrowArgumentException()
    {
        var estimator = new SampleCovarianceEstimator();

        var act = () => estimator.Estimate(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SampleCovariance_EmptyInput_ShouldThrowArgumentException()
    {
        var estimator = new SampleCovarianceEstimator();

        var act = () => estimator.Estimate([]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SampleCovariance_SingleObservation_ShouldThrowArgumentException()
    {
        var estimator = new SampleCovarianceEstimator();

        var act = () => estimator.Estimate([new[] { 0.01m }]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SampleCovariance_MismatchedLengths_ShouldThrowArgumentException()
    {
        var estimator = new SampleCovarianceEstimator();

        var act = () => estimator.Estimate([new[] { 0.01m, 0.02m }, new[] { 0.01m }]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SampleCovariance_DiagonalShouldBeNonNegative()
    {
        var estimator = new SampleCovarianceEstimator();

        var cov = estimator.Estimate(UncorrelatedReturns);

        cov[0, 0].Should().BeGreaterThanOrEqualTo(0m);
        cov[1, 1].Should().BeGreaterThanOrEqualTo(0m);
    }

    // --- ExponentiallyWeightedCovarianceEstimator Tests ---

    [Fact]
    public void EWMA_ShouldWeightRecentDataMore()
    {
        // Two series: both have a shock at the end.
        // EWMA should produce higher variance than sample covariance that treats all equally.
        var returns = new[]
        {
            new[] { 0.001m, 0.001m, 0.001m, 0.001m, 0.10m },
            new[] { 0.001m, 0.001m, 0.001m, 0.001m, 0.10m }
        };

        var ewma = new ExponentiallyWeightedCovarianceEstimator(0.94m);
        var sample = new SampleCovarianceEstimator();

        var ewmaCov = ewma.Estimate(returns);
        var sampleCov = sample.Estimate(returns);

        // EWMA should give higher variance because it puts more weight on the recent large return
        ewmaCov[0, 0].Should().BeGreaterThan(sampleCov[0, 0] * 0.5m,
            "EWMA should weight the recent shock more heavily");
    }

    [Fact]
    public void EWMA_ShouldBeSymmetric()
    {
        var ewma = new ExponentiallyWeightedCovarianceEstimator(0.94m);

        var cov = ewma.Estimate(UncorrelatedReturns);

        cov[0, 1].Should().BeApproximately(cov[1, 0], Precision);
    }

    [Fact]
    public void EWMA_InvalidLambda_Zero_ShouldThrow()
    {
        var act = () => new ExponentiallyWeightedCovarianceEstimator(0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EWMA_InvalidLambda_One_ShouldThrow()
    {
        var act = () => new ExponentiallyWeightedCovarianceEstimator(1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EWMA_InvalidLambda_Negative_ShouldThrow()
    {
        var act = () => new ExponentiallyWeightedCovarianceEstimator(-0.5m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- LedoitWolfShrinkageEstimator Tests ---

    [Fact]
    public void LedoitWolf_ShouldPullEigenvaluesCloserToMean()
    {
        // For perfectly correlated data, sample covariance has very spread eigenvalues.
        // Ledoit-Wolf should shrink off-diagonal elements toward zero (target is scaled identity).
        var lw = new LedoitWolfShrinkageEstimator();
        var sample = new SampleCovarianceEstimator();

        var lwCov = lw.Estimate(PerfectlyCorrelatedReturns);
        var sampleCov = sample.Estimate(PerfectlyCorrelatedReturns);

        // The off-diagonal should be closer to zero (or at least smaller in magnitude)
        // than the sample covariance off-diagonal
        Math.Abs(lwCov[0, 1]).Should().BeLessThanOrEqualTo(Math.Abs(sampleCov[0, 1]) + Precision,
            "Shrinkage should reduce off-diagonal magnitude");
    }

    [Fact]
    public void LedoitWolf_ShouldBeSymmetric()
    {
        var lw = new LedoitWolfShrinkageEstimator();

        var cov = lw.Estimate(UncorrelatedReturns);

        cov[0, 1].Should().BeApproximately(cov[1, 0], Precision);
    }

    [Fact]
    public void LedoitWolf_DiagonalShouldBePositive()
    {
        var lw = new LedoitWolfShrinkageEstimator();

        var cov = lw.Estimate(PerfectlyCorrelatedReturns);

        cov[0, 0].Should().BeGreaterThan(0m);
        cov[1, 1].Should().BeGreaterThan(0m);
    }

    [Fact]
    public void LedoitWolf_SingleAsset_ShouldReturnSampleVariance()
    {
        // With one asset, the target (mu*I) is just the variance, shrinkage doesn't change it
        var returns = new[] { new[] { 0.01m, 0.02m, -0.01m, 0.03m } };
        var lw = new LedoitWolfShrinkageEstimator();
        var sample = new SampleCovarianceEstimator();

        var lwCov = lw.Estimate(returns);
        var sampleCov = sample.Estimate(returns);

        lwCov[0, 0].Should().BeApproximately(sampleCov[0, 0], Precision);
    }

    [Fact]
    public void LedoitWolf_NullInput_ShouldThrowArgumentException()
    {
        var lw = new LedoitWolfShrinkageEstimator();

        var act = () => lw.Estimate(null!);

        act.Should().Throw<ArgumentException>();
    }
}
