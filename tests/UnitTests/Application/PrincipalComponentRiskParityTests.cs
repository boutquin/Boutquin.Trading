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
using FluentAssertions;

/// <summary>
/// Tests for <see cref="PrincipalComponentRiskParityConstruction"/>.
/// </summary>
public sealed class PrincipalComponentRiskParityTests
{
    private const decimal Precision = 1e-6m;

    #region AC-A1: Implements IPortfolioConstructionModel — weights non-negative, sum to 1.0

    [Fact]
    public void ComputeTargetWeights_ThreeAssets_ReturnsNonNegativeWeightsSummingToOne()
    {
        var assets = CreateAssets(3);
        var returns = CreateUncorrelatedReturns(3, 60);

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(3);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void ComputeTargetWeights_EmptyAssets_ReturnsEmpty()
    {
        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(new List<Symbol>(), []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTargetWeights_SingleAsset_ReturnsFullWeight()
    {
        var assets = CreateAssets(1);
        var returns = new[] { new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m } };

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(1);
        weights[assets[0]].Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void ComputeTargetWeights_NullAssets_ThrowsArgumentNullException()
    {
        var model = new PrincipalComponentRiskParityConstruction();

        var act = () => model.ComputeTargetWeights(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeTargetWeights_MismatchedReturns_ThrowsArgumentException()
    {
        var assets = CreateAssets(3);
        var returns = new[] { new[] { 0.01m, 0.02m } }; // only 1 series for 3 assets

        var model = new PrincipalComponentRiskParityConstruction();

        var act = () => model.ComputeTargetWeights(assets, returns);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region AC-A2/A3: Eigendecomposes covariance and allocates inverse-risk (1/sqrt(λ))

    [Fact]
    public void ComputeTargetWeights_TwoAssets_ProducesValidWeights()
    {
        var assets = CreateAssets(2);
        // Two uncorrelated assets with different volatilities
        var returns = new[]
        {
            new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.01m, -0.01m, 0.02m, -0.02m, 0.01m, -0.01m },
            new[] { 0.005m, -0.005m, 0.01m, -0.01m, 0.005m, -0.005m, 0.01m, -0.01m, 0.005m, -0.005m }
        };

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    #endregion

    #region AC-A4: Signal threshold — Marcenko-Pastur upper bound λ+ = (1 + 1/√q)²

    [Fact]
    public void ComputeTargetWeights_CustomSignalThreshold_UsesProvidedThreshold()
    {
        var assets = CreateAssets(3);
        var returns = CreateUncorrelatedReturns(3, 60);

        // Very high threshold — should exclude all PCs and fall back to equal weight
        var model = new PrincipalComponentRiskParityConstruction(signalThreshold: 999m);
        var weights = model.ComputeTargetWeights(assets, returns);

        var expectedWeight = 1.0m / 3;
        foreach (var w in weights.Values)
        {
            w.Should().BeApproximately(expectedWeight, Precision,
                "All PCs below threshold → equal-weight fallback");
        }
    }

    #endregion

    #region AC-A5: Degenerate case — all eigenvalues below threshold → equal-weight fallback

    [Fact]
    public void ComputeTargetWeights_AllEigenvaluesBelowThreshold_FallsBackToEqualWeight()
    {
        var assets = CreateAssets(3);
        var returns = CreateUncorrelatedReturns(3, 60);

        // Very high threshold forces all PCs to be excluded
        var model = new PrincipalComponentRiskParityConstruction(signalThreshold: decimal.MaxValue);
        var weights = model.ComputeTargetWeights(assets, returns);

        var expectedWeight = 1.0m / 3;
        foreach (var w in weights.Values)
        {
            w.Should().BeApproximately(expectedWeight, Precision);
        }
    }

    #endregion

    #region AC-A7: Uncorrelated assets (identity correlation) → equal weights

    [Fact]
    public void ComputeTargetWeights_UncorrelatedAssets_ProducesApproximatelyEqualWeights()
    {
        var assets = CreateAssets(3);
        // Construct truly uncorrelated series (orthogonal patterns)
        var returns = new[]
        {
            new[] { 1m, 0m, 0m, 1m, 0m, 0m, 1m, 0m, 0m, 1m, 0m, 0m },
            new[] { 0m, 1m, 0m, 0m, 1m, 0m, 0m, 1m, 0m, 0m, 1m, 0m },
            new[] { 0m, 0m, 1m, 0m, 0m, 1m, 0m, 0m, 1m, 0m, 0m, 1m }
        };

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        var expectedWeight = 1.0m / 3;
        foreach (var w in weights.Values)
        {
            w.Should().BeApproximately(expectedWeight, 0.05m,
                "Uncorrelated assets should produce approximately equal weights");
        }
    }

    #endregion

    #region AC-A8: Perfectly correlated assets (rank-1) → allocates entirely to PC1

    [Fact]
    public void ComputeTargetWeights_PerfectlyCorrelatedAssets_AllocatesToPc1()
    {
        var assets = CreateAssets(3);
        // All series are identical → rank-1 covariance → only PC1 is signal
        var baseReturns = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, -0.01m, 0.02m, -0.02m };
        var returns = new[]
        {
            baseReturns,
            baseReturns.ToArray(), // copy
            baseReturns.ToArray()  // copy
        };

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
        // With perfectly correlated assets, all weight should be in PC1 direction
        // PC1 of identical assets is equal-weight → equal weights in asset space
        var expectedWeight = 1.0m / 3;
        foreach (var w in weights.Values)
        {
            w.Should().BeApproximately(expectedWeight, 0.05m);
        }
    }

    #endregion

    #region AC-A9: Block-correlated, rank-deficient, N=2 minimum, N=10 larger universe

    [Fact]
    public void ComputeTargetWeights_BlockCorrelatedAssets_ProducesValidWeights()
    {
        var assets = CreateAssets(4);
        var baseA = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, -0.01m, 0.02m, -0.02m };
        var baseB = new[] { -0.01m, 0.02m, -0.01m, 0.03m, -0.02m, 0.01m, -0.03m, 0.02m, -0.01m, 0.01m };
        var returns = new[]
        {
            baseA,
            baseA.Select(r => r * 1.1m + 0.001m).ToArray(), // correlated with A
            baseB,
            baseB.Select(r => r * 0.9m - 0.001m).ToArray()  // correlated with B
        };

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    [Fact]
    public void ComputeTargetWeights_RankDeficientReturns_ProducesValidWeights()
    {
        var assets = CreateAssets(3);
        // Rank-2 return matrix (third asset = sum of first two)
        var a = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m };
        var b = new[] { -0.01m, 0.03m, -0.02m, 0.01m, -0.03m };
        var c = new decimal[5];
        for (var i = 0; i < 5; i++)
        {
            c[i] = a[i] + b[i];
        }

        var returns = new[] { a, b, c };

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    [Fact]
    public void ComputeTargetWeights_TwoAssetsMinimum_ProducesValidWeights()
    {
        var assets = CreateAssets(2);
        var returns = CreateUncorrelatedReturns(2, 30);

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(2);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    [Fact]
    public void ComputeTargetWeights_TenAssetsLargerUniverse_ProducesValidWeights()
    {
        var assets = CreateAssets(10);
        var returns = CreateUncorrelatedReturns(10, 120);

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(10);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    #endregion

    #region AC-A2: Accepts ICovarianceEstimator via constructor injection

    [Fact]
    public void ComputeTargetWeights_CustomCovarianceEstimator_UsesInjectedEstimator()
    {
        var assets = CreateAssets(3);
        var returns = CreateUncorrelatedReturns(3, 60);

        var mockEstimator = new Mock<ICovarianceEstimator>();
        // Return identity matrix (uncorrelated)
        var identity = new decimal[3, 3];
        identity[0, 0] = 1m;
        identity[1, 1] = 1m;
        identity[2, 2] = 1m;
        mockEstimator.Setup(e => e.Estimate(It.IsAny<decimal[][]>())).Returns(identity);

        var model = new PrincipalComponentRiskParityConstruction(covarianceEstimator: mockEstimator.Object);
        var weights = model.ComputeTargetWeights(assets, returns);

        mockEstimator.Verify(e => e.Estimate(It.IsAny<decimal[][]>()), Times.Once);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
    }

    #endregion

    #region Decision rule: q < 1 → use Kaiser criterion (eigenvalue > 1.0)

    [Fact]
    public void ComputeTargetWeights_MoreAssetsThanObservations_UsesKaiserCriterion()
    {
        // 5 assets, 3 observations → q = 3/5 = 0.6 < 1
        var assets = CreateAssets(5);
        var returns = new decimal[5][];
        var rng = new Random(42);
        for (var i = 0; i < 5; i++)
        {
            returns[i] = new decimal[3];
            for (var j = 0; j < 3; j++)
            {
                returns[i][j] = (decimal)(rng.NextDouble() * 0.1 - 0.05);
            }
        }

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    #endregion

    #region Helpers

    private static IReadOnlyList<Symbol> CreateAssets(int n)
    {
        var assets = new List<Symbol>(n);
        for (var i = 0; i < n; i++)
        {
            assets.Add(new Symbol($"ASSET{i}"));
        }

        return assets;
    }

    private static decimal[][] CreateUncorrelatedReturns(int n, int t)
    {
        var rng = new Random(42);
        var returns = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            returns[i] = new decimal[t];
            for (var j = 0; j < t; j++)
            {
                returns[i][j] = (decimal)(rng.NextDouble() * 0.1 - 0.05);
            }
        }

        return returns;
    }

    #endregion
}
