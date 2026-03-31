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

public sealed class DynamicBlackLittermanTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_agg = new("AGG");
    private static readonly Asset s_gld = new("GLD");
    private static readonly Asset s_vnq = new("VNQ");
    private static readonly Asset s_vglt = new("VGLT");

    /// <summary>
    /// Generates synthetic return series with distinct means and some noise.
    /// </summary>
    private static decimal[] MakeReturns(int length, decimal mean, decimal noise)
    {
        var returns = new decimal[length];
        for (var i = 0; i < length; i++)
        {
            returns[i] = mean + noise * ((i % 7) - 3m) / 3m;
        }

        return returns;
    }

    private static IReadOnlyList<BlackLittermanViewSpec> StandardViews() =>
    [
        new(BlackLittermanViewType.Absolute, Asset: "VTI", null, null, 0.07m, 0.6m),
        new(BlackLittermanViewType.Relative, null, LongAsset: "VTI", ShortAsset: "AGG", 0.04m, 0.5m),
        new(BlackLittermanViewType.Absolute, Asset: "GLD", null, null, 0.03m, 0.4m),
    ];

    [Fact]
    public void ComputeTargetWeights_VariableAssetCount_Succeeds()
    {
        // Views reference 5 assets (VTI, AGG, GLD, VNQ, VGLT), but we call with 3.
        var views = new List<BlackLittermanViewSpec>
        {
            new(BlackLittermanViewType.Absolute, "VTI", null, null, 0.07m, 0.6m),
            new(BlackLittermanViewType.Relative, null, "VTI", "AGG", 0.04m, 0.5m),
            new(BlackLittermanViewType.Absolute, "GLD", null, null, 0.03m, 0.4m),
            new(BlackLittermanViewType.Absolute, "VNQ", null, null, 0.05m, 0.5m),
            new(BlackLittermanViewType.Absolute, "VGLT", null, null, 0.02m, 0.3m),
        };

        var model = new DynamicBlackLittermanConstruction(views);

        // Call with only 3 assets — VNQ and VGLT views should be filtered out,
        // VTI-AGG relative view should work.
        var assets = new List<Asset> { s_vti, s_agg, s_gld };
        var returns = new[]
        {
            MakeReturns(60, 0.0004m, 0.01m),
            MakeReturns(60, 0.0001m, 0.005m),
            MakeReturns(60, 0.0002m, 0.008m),
        };

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(3);
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
        foreach (var w in weights.Values)
        {
            w.Should().BeGreaterThanOrEqualTo(0m);
        }
    }

    [Fact]
    public void ComputeTargetWeights_FilteredViews_ExcludesIrrelevantAssets()
    {
        // 3 views: VTI absolute, VTI-AGG relative, GLD absolute.
        // Call with only [VTI, GLD] — AGG absent, so VTI-AGG view should be filtered out.
        var model = new DynamicBlackLittermanConstruction(StandardViews());

        var assets = new List<Asset> { s_vti, s_gld };
        var returns = new[]
        {
            MakeReturns(60, 0.0004m, 0.01m),
            MakeReturns(60, 0.0002m, 0.008m),
        };

        var weights = model.ComputeTargetWeights(assets, returns);

        // Should succeed with 2 views (VTI absolute + GLD absolute)
        weights.Should().HaveCount(2);
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
        weights[s_vti].Should().BeGreaterThan(0m);
        weights[s_gld].Should().BeGreaterThan(0m);
    }

    [Fact]
    public void ComputeTargetWeights_AllAssetsPresent_ProducesValidWeights()
    {
        var model = new DynamicBlackLittermanConstruction(StandardViews());

        var assets = new List<Asset> { s_vti, s_agg, s_gld };
        var returns = new[]
        {
            MakeReturns(60, 0.0004m, 0.01m),
            MakeReturns(60, 0.0001m, 0.005m),
            MakeReturns(60, 0.0002m, 0.008m),
        };

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(3);
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
        foreach (var w in weights.Values)
        {
            w.Should().BeGreaterThanOrEqualTo(0m);
            w.Should().BeLessThanOrEqualTo(1.0m);
        }
    }

    [Fact]
    public void ComputeTargetWeights_NoViewsMatchCurrentAssets_UsesEquilibrium()
    {
        // Views reference VTI, AGG, GLD — but we pass VNQ and VGLT only.
        var model = new DynamicBlackLittermanConstruction(StandardViews());

        var assets = new List<Asset> { s_vnq, s_vglt };
        var returns = new[]
        {
            MakeReturns(60, 0.0003m, 0.012m),
            MakeReturns(60, 0.0001m, 0.006m),
        };

        var weights = model.ComputeTargetWeights(assets, returns);

        // With no views, BL falls back to equilibrium-implied weights.
        // Equilibrium is 1/N = 0.5 each, so posterior is based on implied returns only.
        weights.Should().HaveCount(2);
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
    }

    [Fact]
    public void ComputeTargetWeights_EmptyAssets_ReturnsEmpty()
    {
        var model = new DynamicBlackLittermanConstruction(StandardViews());

        var weights = model.ComputeTargetWeights(new List<Asset>(), []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTargetWeights_WeightConstraintsApplied()
    {
        var model = new DynamicBlackLittermanConstruction(
            StandardViews(),
            minWeight: 0.10m,
            maxWeight: 0.40m);

        var assets = new List<Asset> { s_vti, s_agg, s_gld };
        var returns = new[]
        {
            MakeReturns(60, 0.0004m, 0.01m),
            MakeReturns(60, 0.0001m, 0.005m),
            MakeReturns(60, 0.0002m, 0.008m),
        };

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
        foreach (var w in weights.Values)
        {
            w.Should().BeGreaterThanOrEqualTo(0.10m - 1e-10m);
            w.Should().BeLessThanOrEqualTo(0.40m + 1e-10m);
        }
    }

    /// <summary>
    /// Generates synthetic returns with independent noise to avoid perfect correlation.
    /// Each asset gets a unique deterministic pseudo-random series.
    /// </summary>
    private static decimal[] MakeIndependentReturns(int length, decimal mean, decimal noise, int seed)
    {
        var returns = new decimal[length];
        // Simple LCG for deterministic pseudo-random noise
        var state = (uint)seed;
        for (var i = 0; i < length; i++)
        {
            state = state * 1103515245 + 12345;
            var uniform = (decimal)(state % 10000) / 10000m - 0.5m; // [-0.5, 0.5)
            returns[i] = mean + noise * uniform;
        }

        return returns;
    }

    [Fact]
    public void ComputeTargetWeights_HighConfidenceView_ProducesMeaningfulTilt()
    {
        // Idzorek (2005) Omega: confidence maps to posterior return tilt percentage.
        // With independent (uncorrelated) assets and a strong bullish view on VTI,
        // the mean-variance step should allocate more to VTI.
        var views = new List<BlackLittermanViewSpec>
        {
            new(BlackLittermanViewType.Absolute, Asset: "VTI", null, null, 0.10m, 0.8m),
        };

        var model = new DynamicBlackLittermanConstruction(views);

        var assets = new List<Asset> { s_vti, s_agg, s_gld };
        // Independent return series with different risk profiles
        var returns = new[]
        {
            MakeIndependentReturns(252, 0.0004m, 0.012m, seed: 42),   // VTI: equity
            MakeIndependentReturns(252, 0.0001m, 0.004m, seed: 137),  // AGG: bonds
            MakeIndependentReturns(252, 0.0002m, 0.009m, seed: 271),  // GLD: gold
        };

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);

        // VTI should be above 1/N (33%) due to the high-confidence bullish view.
        // Exact weight depends on covariance structure, but with uncorrelated assets
        // and a strong positive view, VTI should get a meaningful overweight.
        weights[s_vti].Should().BeGreaterThan(1m / 3m,
            "a high-confidence (0.8) bullish absolute view should tilt VTI above 1/N");
    }

    [Fact]
    public void ComputeTargetWeights_ConfidenceOne_DoesNotThrow()
    {
        // Confidence = 1.0 means (1/C - 1) = 0, which previously made omega zero
        // and the M matrix singular. The fix clamps omega to a small positive floor.
        var views = new List<BlackLittermanViewSpec>
        {
            new(BlackLittermanViewType.Absolute, Asset: "VTI", null, null, 0.10m, 1.0m),
        };

        var model = new DynamicBlackLittermanConstruction(views);

        var assets = new List<Asset> { s_vti, s_agg, s_gld };
        var returns = new[]
        {
            MakeIndependentReturns(252, 0.0004m, 0.012m, seed: 42),
            MakeIndependentReturns(252, 0.0001m, 0.004m, seed: 137),
            MakeIndependentReturns(252, 0.0002m, 0.009m, seed: 271),
        };

        // Should not throw (previously threw CalculationException due to singular matrix)
        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(3);
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
        foreach (var w in weights.Values)
        {
            w.Should().BeGreaterThanOrEqualTo(0m);
        }

        // With confidence=1.0, the view should dominate — VTI gets highest weight
        weights[s_vti].Should().BeGreaterThan(1m / 3m,
            "confidence=1.0 view on VTI should tilt VTI above 1/N");
    }

    [Fact]
    public void BlackLittermanViewSpec_InvalidConfidence_Throws()
    {
        var act0 = () => new BlackLittermanViewSpec(BlackLittermanViewType.Absolute, "VTI", null, null, 0.05m, 0m);
        act0.Should().Throw<ArgumentOutOfRangeException>();

        var actNeg = () => new BlackLittermanViewSpec(BlackLittermanViewType.Absolute, "VTI", null, null, 0.05m, -0.1m);
        actNeg.Should().Throw<ArgumentOutOfRangeException>();

        var actOver = () => new BlackLittermanViewSpec(BlackLittermanViewType.Absolute, "VTI", null, null, 0.05m, 1.5m);
        actOver.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ComputeTargetWeights_HigherConfidence_ProducesStrongerTilt()
    {
        // Monotonicity: higher confidence → stronger tilt away from 1/N.
        var lowConfViews = new List<BlackLittermanViewSpec>
        {
            new(BlackLittermanViewType.Absolute, Asset: "VTI", null, null, 0.10m, 0.3m),
        };
        var highConfViews = new List<BlackLittermanViewSpec>
        {
            new(BlackLittermanViewType.Absolute, Asset: "VTI", null, null, 0.10m, 0.8m),
        };

        var lowModel = new DynamicBlackLittermanConstruction(lowConfViews);
        var highModel = new DynamicBlackLittermanConstruction(highConfViews);

        var assets = new List<Asset> { s_vti, s_agg, s_gld };
        var returns = new[]
        {
            MakeIndependentReturns(252, 0.0004m, 0.012m, seed: 42),
            MakeIndependentReturns(252, 0.0001m, 0.004m, seed: 137),
            MakeIndependentReturns(252, 0.0002m, 0.009m, seed: 271),
        };

        var lowWeights = lowModel.ComputeTargetWeights(assets, returns);
        var highWeights = highModel.ComputeTargetWeights(assets, returns);

        // Higher confidence view on VTI should produce higher VTI weight.
        highWeights[s_vti].Should().BeGreaterThan(lowWeights[s_vti],
            "higher confidence should produce stronger tilt toward the viewed asset");
    }
}
