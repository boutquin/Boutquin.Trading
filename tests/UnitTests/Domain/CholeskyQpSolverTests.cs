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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

using Boutquin.Trading.Domain.Helpers;

public sealed class CholeskyQpSolverTests
{
    private const decimal Precision = 1e-10m;

    // ============================================================
    // SolveMinVarianceQP
    // ============================================================

    [Fact]
    public void SolveMinVarianceQP_IdentityCovariance_ShouldReturnEqualWeight()
    {
        var identity = new decimal[,]
        {
            { 1m, 0m, 0m },
            { 0m, 1m, 0m },
            { 0m, 0m, 1m },
        };

        var weights = CholeskyQpSolver.SolveMinVarianceQP(identity, 3, 0m, 1m);

        for (var i = 0; i < 3; i++)
        {
            weights[i].Should().BeApproximately(1m / 3m, Precision);
        }
    }

    [Fact]
    public void SolveMinVarianceQP_DiagonalCovariance_ShouldFavorLowVariance()
    {
        // Asset 0: var=1, Asset 1: var=4, Asset 2: var=9
        var cov = new decimal[,]
        {
            { 1m, 0m, 0m },
            { 0m, 4m, 0m },
            { 0m, 0m, 9m },
        };

        var weights = CholeskyQpSolver.SolveMinVarianceQP(cov, 3, 0m, 1m);

        // Lowest variance asset should get highest weight
        weights[0].Should().BeGreaterThan(weights[1]);
        weights[1].Should().BeGreaterThan(weights[2]);

        // Weights sum to 1
        weights.Sum().Should().BeApproximately(1m, Precision);
    }

    [Fact]
    public void SolveMinVarianceQP_WithBounds_ShouldRespectConstraints()
    {
        var cov = new decimal[,]
        {
            { 1m, 0m },
            { 0m, 100m },
        };

        var weights = CholeskyQpSolver.SolveMinVarianceQP(cov, 2, 0.2m, 0.8m);

        weights[0].Should().BeLessThanOrEqualTo(0.8m + Precision);
        weights[1].Should().BeGreaterThanOrEqualTo(0.2m - Precision);
        weights.Sum().Should().BeApproximately(1m, Precision);
    }

    [Fact]
    public void SolveMinVarianceQP_SingleAsset_ShouldReturnOneHundredPercent()
    {
        var cov = new decimal[,] { { 0.04m } };
        var weights = CholeskyQpSolver.SolveMinVarianceQP(cov, 1, 0m, 1m);
        weights[0].Should().BeApproximately(1m, Precision);
    }

    [Fact]
    public void SolveMinVarianceQP_TwoAssets_ShouldMatchClosedForm()
    {
        // Two uncorrelated assets: var1=1, var2=4
        // Closed-form: w1 = var2/(var1+var2) = 4/5 = 0.8, w2 = 0.2
        var cov = new decimal[,]
        {
            { 1m, 0m },
            { 0m, 4m },
        };

        var weights = CholeskyQpSolver.SolveMinVarianceQP(cov, 2, 0m, 1m);

        weights[0].Should().BeApproximately(0.8m, Precision);
        weights[1].Should().BeApproximately(0.2m, Precision);
    }

    // ============================================================
    // SolveMeanVarianceQP
    // ============================================================

    [Fact]
    public void SolveMeanVarianceQP_HighRiskAversion_ShouldApproachMinVariance()
    {
        var cov = new decimal[,]
        {
            { 1m, 0m },
            { 0m, 4m },
        };
        var means = new[] { 0.10m, 0.20m };

        // With very high risk aversion, should approximate MinVar
        var weights = CholeskyQpSolver.SolveMeanVarianceQP(cov, means, 2, 100m, 0m, 1m);
        var minVarWeights = CholeskyQpSolver.SolveMinVarianceQP(cov, 2, 0m, 1m);

        for (var i = 0; i < 2; i++)
        {
            weights[i].Should().BeApproximately(minVarWeights[i], 0.01m);
        }
    }

    [Fact]
    public void SolveMeanVarianceQP_ZeroRiskAversion_ShouldMaximizeReturn()
    {
        var cov = new decimal[,]
        {
            { 1m, 0m },
            { 0m, 1m },
        };
        var means = new[] { 0.05m, 0.15m };

        // Zero risk aversion: pure max return LP — put 100% in highest-return asset
        var weights = CholeskyQpSolver.SolveMeanVarianceQP(cov, means, 2, 0m, 0m, 1m);

        weights[1].Should().BeApproximately(1m, Precision);
        weights[0].Should().BeApproximately(0m, Precision);
    }

    [Fact]
    public void SolveMeanVarianceQP_WithBounds_ShouldRespectConstraints()
    {
        var cov = new decimal[,]
        {
            { 1m, 0m, 0m },
            { 0m, 1m, 0m },
            { 0m, 0m, 1m },
        };
        var means = new[] { 0.05m, 0.10m, 0.15m };

        var weights = CholeskyQpSolver.SolveMeanVarianceQP(cov, means, 3, 1m, 0.1m, 0.5m);

        foreach (var w in weights)
        {
            w.Should().BeGreaterThanOrEqualTo(0.1m - Precision);
            w.Should().BeLessThanOrEqualTo(0.5m + Precision);
        }

        weights.Sum().Should().BeApproximately(1m, Precision);
    }
}
