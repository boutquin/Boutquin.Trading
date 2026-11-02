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

using Boutquin.Trading.Application.PortfolioConstruction;

public sealed class RiskParityCorrectnessTests : CrossLanguageVerificationBase
{
    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    public void ComputeTargetWeights_Diagonal_ReturnsExactInverseVolatility(int iterations)
    {
        decimal[,] covariance = { { 0.0001m, 0m }, { 0m, 0.0004m } };
        var weights = Solve(covariance, iterations: iterations);
        weights[0].Should().BeApproximately(2m / 3m, 1e-12m);
        AssertEqualRisk(covariance, weights, 1e-10m);
    }

    [Theory]
    [InlineData("-0.00015")]
    [InlineData("0.0001")]
    [InlineData("0.00019999")]
    public void ComputeTargetWeights_TwoAssetsCorrelation_ExactSolution(string cross)
    {
        var offDiagonal = decimal.Parse(cross, System.Globalization.CultureInfo.InvariantCulture);
        decimal[,] covariance = { { 0.0001m, offDiagonal }, { offDiagonal, 0.0004m } };
        var weights = Solve(covariance);
        // Cross terms cancel in w1*(Σw)1 = w2*(Σw)2, for every admissible rho.
        weights[0].Should().BeApproximately(2m / 3m, 1e-10m);
        AssertEqualRisk(covariance, weights, 1e-10m);
    }

    [Fact]
    public void ComputeTargetWeights_ManufacturedSpd_ScaleAndPermutationPreserveExactSolution()
    {
        // A = I + vv', v=(1,-2,1), so A*1=1 and A is SPD.
        // Σ = diag(1/w)*A*diag(1/w) then w_i*(Σw)_i=1 independently of any solver.
        decimal[] expected = [0.2m, 0.3m, 0.5m];
        decimal[] v = [1m, -2m, 1m];
        int[][] permutations = [[0, 1, 2], [2, 0, 1], [1, 2, 0]];
        foreach (var scale in new[] { 1e-20m, 1m, 1e20m })
        {
            foreach (var permutation in permutations)
            {
                var covariance = new decimal[3, 3];
                for (var i = 0; i < 3; i++)
                {
                    for (var j = 0; j < 3; j++)
                    {
                        var a = permutation[i];
                        var b = permutation[j];
                        covariance[i, j] = scale * ((a == b ? 1m : 0m) + v[a] * v[b]) / (expected[a] * expected[b]);
                    }
                }

                var weights = Solve(covariance, iterations: 1000);
                for (var i = 0; i < 3; i++)
                {
                    weights[i].Should().BeApproximately(expected[permutation[i]], 1e-10m);
                }
            }
        }
    }

    [Fact]
    public void ComputeTargetWeights_PerfectPositiveCorrelation_ReturnsPositiveRiskErc()
    {
        decimal[,] covariance = { { 1m, 2m }, { 2m, 4m } };
        var weights = Solve(covariance);
        weights[0].Should().BeApproximately(2m / 3m, 1e-10m);
        AssertEqualRisk(covariance, weights, 1e-10m);
    }

    [Theory]
    [InlineData("0", "0.6", "0.6")]
    [InlineData("0.4", "1", "0.6")]
    [InlineData("0.5", "0.5", "0.5")]
    public void ComputeTargetWeights_BindingBounds_PreservesConfiguredBox(string min, string max, string expected)
    {
        static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        decimal[,] covariance = { { 1m, 0m }, { 0m, 4m } };
        var weights = Solve(covariance, Parse(min), Parse(max));
        weights.Sum().Should().Be(1m);
        weights[0].Should().BeApproximately(Parse(expected), 1e-10m);
        var lower = Parse(min);
        var upper = Parse(max);
        weights.Should().OnlyContain(w => w >= lower && w <= upper);
    }

    [Fact]
    public void ComputeTargetWeights_ThreeAssetBindingCap_FreeRiskContributionsRemainEqual()
    {
        decimal[,] covariance = { { 1m, 0m, 0m }, { 0m, 4m, 0m }, { 0m, 0m, 9m } };
        // First weight capped at .4; remaining inverse-vol weights in ratio 3:2.
        var weights = Solve(covariance, max: 0.4m);
        weights[0].Should().Be(0.4m);
        weights[1].Should().BeApproximately(0.36m, 1e-10m);
        weights[2].Should().BeApproximately(0.24m, 1e-10m);
        weights.Sum().Should().Be(1m);
    }

    [Theory]
    [InlineData("0", "0.25")]
    [InlineData("0.6", "1")]
    public void ComputeTargetWeights_InfeasibleBox_ThrowsWithoutRelaxing(string min, string max)
    {
        var act = () => Solve(new decimal[,] { { 1m, 0m }, { 0m, 4m } },
            decimal.Parse(min, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(max, System.Globalization.CultureInfo.InvariantCulture));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ComputeTargetWeights_IterationExhaustion_ThrowsInsteadOfReturningIterate()
    {
        var act = () => Solve(new decimal[,] { { 1m, 0.3m }, { 0.3m, 4m } }, iterations: 1);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>().WithMessage("*converg*");
    }

    [Fact]
    public void ComputeTargetWeights_InvalidCovariances_Throw()
    {
        decimal[][,] matrices =
        [
            new decimal[,] { { 0m, 0m }, { 0m, 1m } },
            new decimal[,] { { 1m, 2m }, { 2m, 1m } },
            new decimal[,] { { 1m, 0.1m }, { 0.2m, 1m } },
            new decimal[,] { { 1m, -1m }, { -1m, 1m } },
            new decimal[,] { { -1m, 0m }, { 0m, 1m } }
        ];
        foreach (var covariance in matrices)
        {
            var act = () => Solve(covariance);
            act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
        }
    }

    [Fact]
    public void ComputeTargetWeights_IllConditionedHedge_WithExplicitBudgetFindsErc()
    {
        var assets = new[] { new Symbol("A"), new Symbol("B"), new Symbol("C") };
        var returns = R2QuantReviewFixesTestData.NegativeMrcReturns;
        var covariance = new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator().Estimate(returns);
        var result = new RiskParityConstruction(maxIterations: 10000).ComputeTargetWeights(assets, returns);
        AssertEqualRisk(covariance, assets.Select(asset => result[asset]).ToArray(), 1e-10m);
    }

    [Fact]
    public void ComputeTargetWeights_TinyIndefiniteVariance_RejectsRelativeDomainViolation()
    {
        var act = () => Solve(new decimal[,] { { 1e-28m, 2e-14m }, { 2e-14m, 1m } });
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void ComputeTargetWeights_DecimalTolerance_PreservesMoreThanDoublePrecision()
    {
        decimal[,] covariance = { { 1m, 0m }, { 0m, 4m } };
        var weights = Solve(covariance, tolerance: 1e-20m);
        weights[0].Should().BeApproximately(2m / 3m, 1e-20m);
        AssertEqualRisk(covariance, weights, 1e-20m);
    }

    [Fact]
    public void ComputeTargetWeights_MalformedCovarianceOrDuplicateAssets_Fails()
    {
        var assets = new[] { new Symbol("A"), new Symbol("B") };
        decimal[][] returns = [[0.01m, -0.01m], [0.02m, -0.02m]];
        var estimator = new Mock<ICovarianceEstimator>();
        estimator.Setup(e => e.Estimate(It.IsAny<decimal[][]>())).Returns(new decimal[1, 1]);
        var model = new RiskParityConstruction(estimator.Object);
        var shape = () => model.ComputeTargetWeights(assets, returns);
        shape.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
        var duplicate = () => model.ComputeTargetWeights([assets[0], assets[0]], returns);
        duplicate.Should().Throw<ArgumentException>();
        var malformed = () => model.ComputeTargetWeights(assets, [[0.01m], [0.01m]]);
        malformed.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_InvalidControls_Throws()
    {
        Action[] actions =
        [
            () => new RiskParityConstruction(minWeight: -0.1m),
            () => new RiskParityConstruction(maxWeight: 1.1m),
            () => new RiskParityConstruction(minWeight: 0.8m, maxWeight: 0.2m),
            () => new RiskParityConstruction(maxIterations: 0),
            () => new RiskParityConstruction(tolerance: 0m)
        ];
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [Fact]
    public void ComputeTargetWeights_IndependentHighPrecisionOracles_MatchWeights()
    {
        using var document = LoadVector("risk_parity_independent");
        foreach (var testCase in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var rows = testCase.GetProperty("covariance").EnumerateArray().ToArray();
            var covariance = new decimal[rows.Length, rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                for (var j = 0; j < rows.Length; j++)
                {
                    covariance[i, j] = rows[i][j].GetDecimal();
                }
            }

            var weights = Solve(covariance, testCase.GetProperty("min").GetDecimal(), testCase.GetProperty("max").GetDecimal());
            var expected = testCase.GetProperty("weights").EnumerateArray()
                .Select(x => decimal.Parse(x.GetString()!, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            for (var i = 0; i < weights.Length; i++)
            {
                weights[i].Should().BeApproximately(expected[i], 1e-10m, testCase.GetProperty("name").GetString());
            }
        }
    }

    private static decimal[] Solve(decimal[,] covariance, decimal min = 0m, decimal max = 1m, int iterations = 100, decimal tolerance = 1e-10m)
    {
        var n = covariance.GetLength(0);
        var estimator = new Mock<ICovarianceEstimator>();
        estimator.Setup(e => e.Estimate(It.IsAny<decimal[][]>())).Returns(covariance);
        var model = new RiskParityConstruction(estimator.Object, min, max, iterations, tolerance);
        var assets = Enumerable.Range(0, n).Select(i => new Symbol($"A{i}")).ToArray();
        var returns = Enumerable.Range(0, n).Select(_ => new[] { 0.01m, -0.01m }).ToArray();
        var result = model.ComputeTargetWeights(assets, returns);
        return assets.Select(asset => result[asset]).ToArray();
    }

    private static void AssertEqualRisk(decimal[,] covariance, decimal[] weights, decimal tolerance)
    {
        var contributions = new decimal[weights.Length];
        for (var i = 0; i < weights.Length; i++)
        {
            for (var j = 0; j < weights.Length; j++)
            {
                contributions[i] += weights[i] * covariance[i, j] * weights[j];
            }
        }

        var variance = contributions.Sum();
        variance.Should().BeGreaterThan(0m);
        foreach (var contribution in contributions)
        {
            (contribution / variance).Should().BeApproximately(1m / weights.Length, tolerance);
        }
    }
}
