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

public sealed class WeightConstrainedConstructionTests
{
    private const decimal Precision = 1e-10m;

    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_gld = new("GLD");

    private static decimal[][] ThreeAssetReturns =>
    [
        [0.01m, 0.02m, -0.01m, 0.005m],
        [-0.005m, 0.01m, 0.015m, -0.002m],
        [0.008m, -0.003m, 0.012m, 0.001m],
    ];

    // ============================================================
    // Happy path: floors + caps clamp and renormalize
    // ============================================================

    [Fact]
    public void ComputeTargetWeights_WithFloors_ShouldEnforceMinimumWeights()
    {
        var inner = new EqualWeightConstruction(); // 1/3 each
        var floors = new Dictionary<Asset, decimal>
        {
            [s_vti] = 0.50m, // force VTI to at least 50%
        };

        var sut = new WeightConstrainedConstruction(inner, floors);
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };
        var weights = sut.ComputeTargetWeights(assets, ThreeAssetReturns);

        weights[s_vti].Should().BeGreaterThanOrEqualTo(0.50m - Precision);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        foreach (var w in weights.Values)
        {
            w.Should().BeGreaterThanOrEqualTo(0m);
        }
    }

    [Fact]
    public void ComputeTargetWeights_WithCaps_ShouldEnforceMaximumWeights()
    {
        var inner = new EqualWeightConstruction(); // 1/3 each
        var caps = new Dictionary<Asset, decimal>
        {
            [s_vti] = 0.20m, // force VTI to at most 20%
        };

        var sut = new WeightConstrainedConstruction(inner, caps: caps);
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };
        var weights = sut.ComputeTargetWeights(assets, ThreeAssetReturns);

        weights[s_vti].Should().BeLessThanOrEqualTo(0.20m + Precision);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void ComputeTargetWeights_WithFloorAndCap_ShouldClampBoth()
    {
        var inner = new EqualWeightConstruction();
        var floors = new Dictionary<Asset, decimal>
        {
            [s_tlt] = 0.40m,
        };
        var caps = new Dictionary<Asset, decimal>
        {
            [s_vti] = 0.20m,
        };

        var sut = new WeightConstrainedConstruction(inner, floors, caps);
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };
        var weights = sut.ComputeTargetWeights(assets, ThreeAssetReturns);

        weights[s_vti].Should().BeLessThanOrEqualTo(0.20m + Precision);
        weights[s_tlt].Should().BeGreaterThanOrEqualTo(0.40m - Precision);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void ComputeTargetWeights_WithAssetWeightConstraints_ShouldWork()
    {
        var inner = new EqualWeightConstruction();
        var constraints = new AssetWeightConstraints(
            Floors: new Dictionary<Asset, decimal> { [s_tlt] = 0.50m },
            Caps: new Dictionary<Asset, decimal> { [s_vti] = 0.15m });

        var sut = new WeightConstrainedConstruction(inner, constraints);
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };
        var weights = sut.ComputeTargetWeights(assets, ThreeAssetReturns);

        weights[s_tlt].Should().BeGreaterThanOrEqualTo(0.50m - Precision);
        weights[s_vti].Should().BeLessThanOrEqualTo(0.15m + Precision);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
    }

    // ============================================================
    // No constraints = passthrough
    // ============================================================

    [Fact]
    public void ComputeTargetWeights_NoConstraints_ShouldReturnInnerWeights()
    {
        var inner = new EqualWeightConstruction();
        var sut = new WeightConstrainedConstruction(inner);
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };
        var weights = sut.ComputeTargetWeights(assets, ThreeAssetReturns);

        weights[s_vti].Should().BeApproximately(1m / 3m, Precision);
        weights[s_tlt].Should().BeApproximately(1m / 3m, Precision);
        weights[s_gld].Should().BeApproximately(1m / 3m, Precision);
    }

    // ============================================================
    // Empty assets
    // ============================================================

    [Fact]
    public void ComputeTargetWeights_EmptyAssets_ShouldReturnEmptyWeights()
    {
        var inner = new EqualWeightConstruction();
        var sut = new WeightConstrainedConstruction(inner);
        var weights = sut.ComputeTargetWeights(new List<Asset>(), []);

        weights.Should().BeEmpty();
    }

    // ============================================================
    // Validation: floor > cap should throw
    // ============================================================

    [Fact]
    public void Constructor_FloorGreaterThanCap_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();
        var floors = new Dictionary<Asset, decimal> { [s_vti] = 0.60m };
        var caps = new Dictionary<Asset, decimal> { [s_vti] = 0.20m };

        var act = () => new WeightConstrainedConstruction(inner, floors, caps);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeFloor_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();
        var floors = new Dictionary<Asset, decimal> { [s_vti] = -0.1m };

        var act = () => new WeightConstrainedConstruction(inner, floors);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_CapGreaterThanOne_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();
        var caps = new Dictionary<Asset, decimal> { [s_vti] = 1.5m };

        var act = () => new WeightConstrainedConstruction(inner, caps: caps);
        act.Should().Throw<ArgumentException>();
    }

    // ============================================================
    // Single asset
    // ============================================================

    [Fact]
    public void ComputeTargetWeights_SingleAsset_ShouldReturnOneHundredPercent()
    {
        var inner = new EqualWeightConstruction();
        var sut = new WeightConstrainedConstruction(inner,
            floors: new Dictionary<Asset, decimal> { [s_vti] = 0.5m },
            caps: new Dictionary<Asset, decimal> { [s_vti] = 1.0m });

        var assets = new List<Asset> { s_vti };
        var weights = sut.ComputeTargetWeights(assets, [[0.01m, 0.02m]]);

        weights[s_vti].Should().BeApproximately(1.0m, Precision);
    }
}
