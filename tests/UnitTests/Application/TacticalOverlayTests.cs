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
using Boutquin.Trading.Domain.Enums;

public sealed class TacticalOverlayTests
{
    private const decimal Precision = 1e-6m;

    [Fact]
    public void TacticalOverlay_RiskOffRegime_ShouldReduceEquityWeight()
    {
        var vti = new Asset("VTI");
        var tlt = new Asset("TLT");
        var assets = new List<Asset> { vti, tlt };
        var returns = new[] { new[] { 0.01m, -0.01m }, new[] { -0.005m, 0.005m } };

        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, returns))
            .Returns(new Dictionary<Asset, decimal> { [vti] = 0.6m, [tlt] = 0.4m });

        var regimeTilts = new Dictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>>
        {
            [EconomicRegime.FallingGrowthFallingInflation] = new Dictionary<Asset, decimal>
            {
                [vti] = -0.2m, // Reduce equity
                [tlt] = 0.2m,  // Increase bonds
            }
        };

        var overlay = new TacticalOverlayConstruction(
            baseModel.Object,
            regimeTilts,
            EconomicRegime.FallingGrowthFallingInflation);

        var weights = overlay.ComputeTargetWeights(assets, returns);

        // Raw: VTI = 0.6 - 0.2 = 0.4, TLT = 0.4 + 0.2 = 0.6 → already sums to 1.0
        weights[vti].Should().BeApproximately(0.4m, Precision);
        weights[tlt].Should().BeApproximately(0.6m, Precision);
    }

    [Fact]
    public void TacticalOverlay_MomentumOverweight_ShouldAdjustWeights()
    {
        var vti = new Asset("VTI");
        var tlt = new Asset("TLT");
        var assets = new List<Asset> { vti, tlt };
        var returns = new[] { new[] { 0.01m }, new[] { -0.01m } };

        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, returns))
            .Returns(new Dictionary<Asset, decimal> { [vti] = 0.5m, [tlt] = 0.5m });

        var momentumScores = new Dictionary<Asset, decimal>
        {
            [vti] = 0.5m,  // Positive momentum
            [tlt] = -0.5m, // Negative momentum
        };

        // Must include current regime in tilts (empty tilts = no regime adjustment, momentum only)
        var regimeTilts = new Dictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new Dictionary<Asset, decimal>()
        };

        var overlay = new TacticalOverlayConstruction(
            baseModel.Object,
            regimeTilts,
            EconomicRegime.RisingGrowthRisingInflation,
            momentumScores,
            momentumStrength: 0.2m);

        var weights = overlay.ComputeTargetWeights(assets, returns);

        // VTI: 0.5 + 0.5*0.2 = 0.6, TLT: 0.5 + (-0.5)*0.2 = 0.4. Sum = 1.0
        weights[vti].Should().BeApproximately(0.6m, Precision);
        weights[tlt].Should().BeApproximately(0.4m, Precision);
    }

    [Fact]
    public void TacticalOverlay_WeightsSumToOne()
    {
        var a = new Asset("A");
        var b = new Asset("B");
        var c = new Asset("C");
        var assets = new List<Asset> { a, b, c };
        var returns = new[] { new[] { 0.01m }, new[] { -0.01m }, new[] { 0.005m } };

        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, returns))
            .Returns(new Dictionary<Asset, decimal> { [a] = 0.5m, [b] = 0.3m, [c] = 0.2m });

        var regimeTilts = new Dictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new Dictionary<Asset, decimal>
            {
                [a] = 0.1m,
                [b] = -0.3m,
                [c] = 0.05m
            }
        };

        var overlay = new TacticalOverlayConstruction(
            baseModel.Object, regimeTilts, EconomicRegime.RisingGrowthRisingInflation);

        var weights = overlay.ComputeTargetWeights(assets, returns);
        weights.Values.Sum().Should().BeApproximately(1.0m, Precision);
        weights.Values.All(w => w >= 0m).Should().BeTrue();
    }

    [Fact]
    public void TacticalOverlay_EmptyAssets_ShouldReturnEmpty()
    {
        var baseModel = new Mock<IPortfolioConstructionModel>();
        // Must include current regime in tilts (empty tilts = no adjustment)
        var regimeTilts = new Dictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new Dictionary<Asset, decimal>()
        };
        var overlay = new TacticalOverlayConstruction(
            baseModel.Object,
            regimeTilts,
            EconomicRegime.RisingGrowthRisingInflation);

        var weights = overlay.ComputeTargetWeights(new List<Asset>(), Array.Empty<decimal[]>());
        weights.Should().BeEmpty();
    }
}
