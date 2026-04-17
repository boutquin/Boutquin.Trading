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

using Boutquin.Numerics.Solvers;

public sealed class ActiveSetQpSolverTests
{
    private const decimal Precision = 1e-10m;

    // ============================================================
    // SolveMinVariance
    // ============================================================

    [Fact]
    public void SolveMinVariance_IdentityCovariance_ShouldReturnEqualWeight()
    {
        var identity = new decimal[,]
        {
            { 1m, 0m, 0m },
            { 0m, 1m, 0m },
            { 0m, 0m, 1m },
        };

        var weights = ActiveSetQpSolver.SolveMinVariance(identity, 0m, 1m);

        for (var i = 0; i < 3; i++)
        {
            weights[i].Should().BeApproximately(1m / 3m, Precision);
        }
    }

    [Fact]
    public void SolveMinVariance_DiagonalCovariance_ShouldFavorLowVariance()
    {
        // Symbol 0: var=1, Symbol 1: var=4, Symbol 2: var=9
        var cov = new decimal[,]
        {
            { 1m, 0m, 0m },
            { 0m, 4m, 0m },
            { 0m, 0m, 9m },
        };

        var weights = ActiveSetQpSolver.SolveMinVariance(cov, 0m, 1m);

        // Lowest variance asset should get highest weight
        weights[0].Should().BeGreaterThan(weights[1]);
        weights[1].Should().BeGreaterThan(weights[2]);

        // Weights sum to 1
        weights.Sum().Should().BeApproximately(1m, Precision);
    }

    [Fact]
    public void SolveMinVariance_WithBounds_ShouldRespectConstraints()
    {
        var cov = new decimal[,]
        {
            { 1m, 0m },
            { 0m, 100m },
        };

        var weights = ActiveSetQpSolver.SolveMinVariance(cov, 0.2m, 0.8m);

        weights[0].Should().BeLessThanOrEqualTo(0.8m + Precision);
        weights[1].Should().BeGreaterThanOrEqualTo(0.2m - Precision);
        weights.Sum().Should().BeApproximately(1m, Precision);
    }

    [Fact]
    public void SolveMinVariance_SingleAsset_ShouldReturnOneHundredPercent()
    {
        var cov = new decimal[,] { { 0.04m } };
        var weights = ActiveSetQpSolver.SolveMinVariance(cov, 0m, 1m);
        weights[0].Should().BeApproximately(1m, Precision);
    }

    [Fact]
    public void SolveMinVariance_TwoAssets_ShouldMatchClosedForm()
    {
        // Two uncorrelated assets: var1=1, var2=4
        // Closed-form: w1 = var2/(var1+var2) = 4/5 = 0.8, w2 = 0.2
        var cov = new decimal[,]
        {
            { 1m, 0m },
            { 0m, 4m },
        };

        var weights = ActiveSetQpSolver.SolveMinVariance(cov, 0m, 1m);

        weights[0].Should().BeApproximately(0.8m, Precision);
        weights[1].Should().BeApproximately(0.2m, Precision);
    }

    // ============================================================
    // SolveMeanVariance
    // ============================================================

    [Fact]
    public void SolveMeanVariance_HighRiskAversion_ShouldApproachMinVariance()
    {
        var cov = new decimal[,]
        {
            { 1m, 0m },
            { 0m, 4m },
        };
        var means = new[] { 0.10m, 0.20m };

        // With very high risk aversion, should approximate MinVar
        var weights = ActiveSetQpSolver.SolveMeanVariance(cov, means, 100m, 0m, 1m);
        var minVarWeights = ActiveSetQpSolver.SolveMinVariance(cov, 0m, 1m);

        for (var i = 0; i < 2; i++)
        {
            weights[i].Should().BeApproximately(minVarWeights[i], 0.01m);
        }
    }

    [Fact]
    public void SolveMeanVariance_ZeroRiskAversion_ShouldMaximizeReturn()
    {
        var cov = new decimal[,]
        {
            { 1m, 0m },
            { 0m, 1m },
        };
        var means = new[] { 0.05m, 0.15m };

        // Zero risk aversion: pure max return LP — put 100% in highest-return asset
        var weights = ActiveSetQpSolver.SolveMeanVariance(cov, means, 0m, 0m, 1m);

        weights[1].Should().BeApproximately(1m, Precision);
        weights[0].Should().BeApproximately(0m, Precision);
    }

    [Fact]
    public void SolveMeanVariance_WithBounds_ShouldRespectConstraints()
    {
        var cov = new decimal[,]
        {
            { 1m, 0m, 0m },
            { 0m, 1m, 0m },
            { 0m, 0m, 1m },
        };
        var means = new[] { 0.05m, 0.10m, 0.15m };

        var weights = ActiveSetQpSolver.SolveMeanVariance(cov, means, 1m, 0.1m, 0.5m);

        foreach (var w in weights)
        {
            w.Should().BeGreaterThanOrEqualTo(0.1m - Precision);
            w.Should().BeLessThanOrEqualTo(0.5m + Precision);
        }

        weights.Sum().Should().BeApproximately(1m, Precision);
    }
}
