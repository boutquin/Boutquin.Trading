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

public sealed class RegimeWeightConstrainedConstructionTests
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
    // Regime-specific constraints applied correctly
    // ============================================================

    [Fact]
    public void ComputeTargetWeights_RisingGrowthRisingInflation_ShouldApplyRegimeConstraints()
    {
        var inner = new EqualWeightConstruction();
        var regime = EconomicRegime.RisingGrowthRisingInflation;

        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [regime] = new AssetWeightConstraints(
                Floors: new Dictionary<Asset, decimal> { [s_gld] = 0.50m },
                Caps: new Dictionary<Asset, decimal> { [s_vti] = 0.20m }),
        };

        var sut = new RegimeWeightConstrainedConstruction(inner, regimeConstraints, regime);
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };
        var weights = sut.ComputeTargetWeights(assets, ThreeAssetReturns);

        weights[s_gld].Should().BeGreaterThanOrEqualTo(0.50m - Precision);
        weights[s_vti].Should().BeLessThanOrEqualTo(0.20m + Precision);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void ComputeTargetWeights_RegimeWithEmptyConstraints_ShouldPassthrough()
    {
        var inner = new EqualWeightConstruction();
        var regime = EconomicRegime.FallingGrowthFallingInflation;

        // All regimes present but current one has empty constraints — passthrough
        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new AssetWeightConstraints(
                Floors: new Dictionary<Asset, decimal> { [s_gld] = 0.80m }),
            [EconomicRegime.RisingGrowthFallingInflation] = new AssetWeightConstraints(),
            [EconomicRegime.FallingGrowthRisingInflation] = new AssetWeightConstraints(),
            [EconomicRegime.FallingGrowthFallingInflation] = new AssetWeightConstraints(),
        };

        var sut = new RegimeWeightConstrainedConstruction(inner, regimeConstraints, regime);
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };
        var weights = sut.ComputeTargetWeights(assets, ThreeAssetReturns);

        weights[s_vti].Should().BeApproximately(1m / 3m, Precision);
        weights[s_tlt].Should().BeApproximately(1m / 3m, Precision);
        weights[s_gld].Should().BeApproximately(1m / 3m, Precision);
    }

    [Fact]
    public void ComputeTargetWeights_AllFourRegimes_ShouldApplyCorrectly()
    {
        var inner = new EqualWeightConstruction();
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };

        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new AssetWeightConstraints(
                Caps: new Dictionary<Asset, decimal> { [s_tlt] = 0.10m }),
            [EconomicRegime.RisingGrowthFallingInflation] = new AssetWeightConstraints(
                Floors: new Dictionary<Asset, decimal> { [s_vti] = 0.60m }),
            [EconomicRegime.FallingGrowthRisingInflation] = new AssetWeightConstraints(
                Floors: new Dictionary<Asset, decimal> { [s_gld] = 0.60m }),
            [EconomicRegime.FallingGrowthFallingInflation] = new AssetWeightConstraints(
                Floors: new Dictionary<Asset, decimal> { [s_tlt] = 0.60m }),
        };

        foreach (var regime in Enum.GetValues<EconomicRegime>())
        {
            var sut = new RegimeWeightConstrainedConstruction(inner, regimeConstraints, regime);
            var weights = sut.ComputeTargetWeights(assets, ThreeAssetReturns);

            weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
            foreach (var w in weights.Values)
            {
                w.Should().BeGreaterThanOrEqualTo(0m);
            }
        }
    }

    // ============================================================
    // Empty regime constraints
    // ============================================================

    [Fact]
    public void Constructor_MissingRegime_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();
        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>();

        var act = () => new RegimeWeightConstrainedConstruction(
            inner, regimeConstraints, EconomicRegime.RisingGrowthRisingInflation);

        act.Should().Throw<ArgumentException>();
    }
}
