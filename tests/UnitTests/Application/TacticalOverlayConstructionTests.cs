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
using FluentAssertions;

/// <summary>
/// Tests for <see cref="TacticalOverlayConstruction"/>.
/// </summary>
public sealed class TacticalOverlayConstructionTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");

    /// <summary>
    /// Regression: Constructor must throw when the current regime has no entry in regimeTilts.
    /// Previously, a missing regime silently skipped all tilts, producing base-model-only weights
    /// without any warning — the same class of bug as DynamicWeightPositionSizer's silent fallback.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrow_WhenRegimeTiltsMissingCurrentRegime()
    {
        // Arrange — regimeTilts has RisingGrowthRisingInflation but currentRegime is FallingGrowthFallingInflation
        var baseModel = new Mock<IPortfolioConstructionModel>();
        var regimeTilts = new Dictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new Dictionary<Asset, decimal>
            {
                [s_vti] = 0.05m,
                [s_tlt] = -0.05m
            }
        };

        // Act & Assert — FallingGrowthFallingInflation is not in regimeTilts
        var act = () => new TacticalOverlayConstruction(
            baseModel.Object,
            regimeTilts,
            EconomicRegime.FallingGrowthFallingInflation);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*FallingGrowthFallingInflation*");
    }

    /// <summary>
    /// Verifies that tilts are actually applied when the regime is present.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_ShouldApplyTilts_WhenRegimePresent()
    {
        // Arrange — base model returns equal weight, regime tilts VTI +10%, TLT -10%
        var assets = new List<Asset> { s_vti, s_tlt };
        var returns = new[] { new[] { 0.01m, -0.01m }, new[] { -0.005m, 0.005m } };

        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, returns))
            .Returns(new Dictionary<Asset, decimal> { [s_vti] = 0.5m, [s_tlt] = 0.5m });

        var regimeTilts = new Dictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new Dictionary<Asset, decimal>
            {
                [s_vti] = 0.10m,
                [s_tlt] = -0.10m
            }
        };

        var overlay = new TacticalOverlayConstruction(
            baseModel.Object,
            regimeTilts,
            EconomicRegime.RisingGrowthRisingInflation);

        // Act
        var weights = overlay.ComputeTargetWeights(assets, returns);

        // Assert — VTI should be overweighted relative to TLT after tilts + renormalization
        // Pre-normalize: VTI = 0.5 + 0.1 = 0.6, TLT = 0.5 - 0.1 = 0.4, total = 1.0
        // After normalize: VTI = 0.6, TLT = 0.4
        weights[s_vti].Should().BeApproximately(0.6m, 1e-10m);
        weights[s_tlt].Should().BeApproximately(0.4m, 1e-10m);
    }
}
