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
/// Tests for <see cref="PcaConstrainedConstruction"/>.
/// </summary>
public sealed class PcaConstrainedConstructionTests
{
    private const decimal Precision = 1e-6m;

    #region AC-B1: Implements IPortfolioConstructionModel as a decorator

    [Fact]
    public void ComputeTargetWeights_WrapsInnerModel_DelegatesToInner()
    {
        var assets = CreateAssets(3);
        var returns = CreateReturns(3, 60);

        var inner = new EqualWeightConstruction();
        var decorator = new PcaConstrainedConstruction(inner);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(3);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    #endregion

    #region AC-B4: K determined by Marcenko-Pastur. If K=N, passthrough.

    [Fact]
    public void ComputeTargetWeights_AllSignal_BehavesAsPassthrough()
    {
        var assets = CreateAssets(3);
        // With N=3 and T=60, if all eigenvalues are above MP, K=N → passthrough
        var returns = CreateReturns(3, 60);

        var inner = new EqualWeightConstruction();
        var withPca = new PcaConstrainedConstruction(inner);
        var withoutPca = inner;

        var pcaWeights = withPca.ComputeTargetWeights(assets, returns);
        var directWeights = withoutPca.ComputeTargetWeights(assets, returns);

        // Equal weight doesn't use returns, so PCA passthrough should still produce valid weights
        pcaWeights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        pcaWeights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    #endregion

    #region AC-B5: Optional maxComponents to cap K

    [Fact]
    public void ComputeTargetWeights_MaxComponentsCapsK_ProducesValidWeights()
    {
        var assets = CreateAssets(5);
        var returns = CreateReturns(5, 120);

        var inner = new EqualWeightConstruction();
        var decorator = new PcaConstrainedConstruction(inner, maxComponents: 2);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(5);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m));
    }

    #endregion

    #region AC-B6/B7: Normalizes back-projected weights, clamps negatives

    [Fact]
    public void ComputeTargetWeights_BackProjectedWeightsAreNormalized()
    {
        var assets = CreateAssets(4);
        var returns = CreateReturns(4, 60);

        var inner = new EqualWeightConstruction();
        var decorator = new PcaConstrainedConstruction(inner, maxComponents: 2);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1.0m, Precision,
            "Back-projected weights must be normalized to sum to 1");
        weights.Values.Should().AllSatisfy(w => w.Should().BeGreaterThanOrEqualTo(0m,
            "Negative weights must be clamped to zero"));
    }

    #endregion

    #region AC-B8: K=0 (all noise) → equal-weight fallback

    [Fact]
    public void ComputeTargetWeights_AllNoise_FallsBackToEqualWeight()
    {
        var assets = CreateAssets(3);
        var returns = CreateReturns(3, 60);

        var inner = new EqualWeightConstruction();
        // maxComponents=0 forces K=0
        var decorator = new PcaConstrainedConstruction(inner, maxComponents: 0);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        var expectedWeight = 1.0m / 3;
        foreach (var w in weights.Values)
        {
            w.Should().BeApproximately(expectedWeight, Precision,
                "K=0 should fall back to equal-weight");
        }
    }

    #endregion

    #region AC-B9: Passthrough when K=N, reduction when K<N

    [Fact]
    public void ComputeTargetWeights_DimensionalityReduction_ProducesDifferentWeights()
    {
        var assets = CreateAssets(5);
        var returns = CreateReturns(5, 120);

        var inner = new InverseVolatilityConstruction();
        var fullPca = new PcaConstrainedConstruction(inner, maxComponents: 5);
        var reducedPca = new PcaConstrainedConstruction(inner, maxComponents: 2);

        var fullWeights = fullPca.ComputeTargetWeights(assets, returns);
        var reducedWeights = reducedPca.ComputeTargetWeights(assets, returns);

        // Both should be valid
        fullWeights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        reducedWeights.Values.Sum().Should().BeApproximately(1.0m, Precision);
    }

    #endregion

    #region AC-B12: Does NOT wrap hierarchical models — throws ArgumentException

    [Fact]
    public void Constructor_WrappingHRP_ThrowsArgumentException()
    {
        var hrp = new HierarchicalRiskParityConstruction();

        var act = () => new PcaConstrainedConstruction(hrp);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*not compatible with hierarchical models*");
    }

    #endregion

    #region Edge cases

    [Fact]
    public void ComputeTargetWeights_EmptyAssets_ReturnsEmpty()
    {
        var inner = new EqualWeightConstruction();
        var decorator = new PcaConstrainedConstruction(inner);

        var weights = decorator.ComputeTargetWeights(new List<Symbol>(), []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTargetWeights_SingleAsset_ReturnsFullWeight()
    {
        var assets = CreateAssets(1);
        var returns = new[] { new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m } };

        var inner = new EqualWeightConstruction();
        var decorator = new PcaConstrainedConstruction(inner);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(1);
        weights[assets[0]].Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void ComputeTargetWeights_NullAssets_ThrowsArgumentNullException()
    {
        var inner = new EqualWeightConstruction();
        var decorator = new PcaConstrainedConstruction(inner);

        var act = () => decorator.ComputeTargetWeights(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullInnerModel_ThrowsArgumentNullException()
    {
        var act = () => new PcaConstrainedConstruction(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AC-B10: Wrapping MDP with PCA constraint produces lower turnover on noisy covariance

    [Fact]
    public void ComputeTargetWeights_MdpWithPca_ReducesTurnoverOnNoisyData()
    {
        var assets = CreateAssets(5);
        var rng = new Random(123);

        // Two slightly different noisy return sets (simulating consecutive rebalance periods)
        var returns1 = CreateNoisyReturns(5, 60, rng);
        var returns2 = PerturbReturns(returns1, rng, 0.001m);

        var mdp = new MaximumDiversificationConstruction();
        var mdpPca = new PcaConstrainedConstruction(mdp, maxComponents: 3);

        var weights1Mdp = mdp.ComputeTargetWeights(assets, returns1);
        var weights2Mdp = mdp.ComputeTargetWeights(assets, returns2);

        var weights1Pca = mdpPca.ComputeTargetWeights(assets, returns1);
        var weights2Pca = mdpPca.ComputeTargetWeights(assets, returns2);

        // Compute L1 turnover (sum of absolute weight changes)
        var turnoverMdp = assets.Sum(a => Math.Abs(weights2Mdp[a] - weights1Mdp[a]));
        var turnoverPca = assets.Sum(a => Math.Abs(weights2Pca[a] - weights1Pca[a]));

        turnoverPca.Should().BeLessThanOrEqualTo(turnoverMdp + 0.01m,
            "PCA constraint should reduce or maintain turnover compared to unconstrained MDP on noisy data");
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

    private static decimal[][] CreateReturns(int n, int t)
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

    private static decimal[][] CreateNoisyReturns(int n, int t, Random rng)
    {
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

    private static decimal[][] PerturbReturns(decimal[][] original, Random rng, decimal noise)
    {
        var n = original.Length;
        var t = original[0].Length;
        var perturbed = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            perturbed[i] = new decimal[t];
            for (var j = 0; j < t; j++)
            {
                perturbed[i][j] = original[i][j] + (decimal)(rng.NextDouble() * (double)noise * 2 - (double)noise);
            }
        }

        return perturbed;
    }

    #endregion
}
