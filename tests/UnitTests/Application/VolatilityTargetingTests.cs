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

public sealed class VolatilityTargetingTests
{
    [Fact]
    public void VolTarget_HigherVolThanTarget_ShouldScaleDown()
    {
        var vti = new Asset("VTI");
        var assets = new List<Asset> { vti };

        // Create returns with known volatility
        // 20 days of alternating +2%/-2% → daily vol ≈ 2%
        var returns = new decimal[20];
        for (var i = 0; i < 20; i++)
        {
            returns[i] = i % 2 == 0 ? 0.02m : -0.02m;
        }

        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, It.IsAny<decimal[][]>()))
            .Returns(new Dictionary<Asset, decimal> { [vti] = 1.0m });

        // Target 10% vol. Realized ≈ 2% daily → ~31.7% annualized
        // Scale factor = 10/31.7 ≈ 0.316
        var volTarget = new VolatilityTargetingConstruction(
            baseModel.Object, targetVolatility: 0.10m, maxLeverage: 1.0m);

        var weights = volTarget.ComputeTargetWeights(assets, new[] { returns });

        weights[vti].Should().BeLessThan(1.0m);
        weights[vti].Should().BeGreaterThan(0m);
    }

    [Fact]
    public void VolTarget_LowerVolThanTarget_ShouldScaleUpWithinLeverage()
    {
        var a = new Asset("A");
        var assets = new List<Asset> { a };

        // Very low vol returns: 0.001, -0.001 alternating (0.1% daily)
        var returns = new decimal[60];
        for (var i = 0; i < 60; i++)
        {
            returns[i] = i % 2 == 0 ? 0.001m : -0.001m;
        }

        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, It.IsAny<decimal[][]>()))
            .Returns(new Dictionary<Asset, decimal> { [a] = 1.0m });

        // Target 20% vol, max leverage 1.5
        var volTarget = new VolatilityTargetingConstruction(
            baseModel.Object, targetVolatility: 0.20m, maxLeverage: 1.5m);

        var weights = volTarget.ComputeTargetWeights(assets, new[] { returns });

        // Scale factor would be > 1 but capped at 1.5
        weights[a].Should().BeInRange(0m, 1.5m);
        weights[a].Should().BeGreaterThan(1.0m);
    }

    [Fact]
    public void VolTarget_EmptyAssets_ShouldReturnEmpty()
    {
        var baseModel = new Mock<IPortfolioConstructionModel>();
        var volTarget = new VolatilityTargetingConstruction(baseModel.Object, 0.10m);

        var weights = volTarget.ComputeTargetWeights(new List<Asset>(), Array.Empty<decimal[]>());
        weights.Should().BeEmpty();
    }

    [Fact]
    public void VolTarget_InsufficientReturns_ShouldReturnBaseWeights()
    {
        var a = new Asset("A");
        var assets = new List<Asset> { a };
        var returns = new[] { new[] { 0.01m } }; // Only 1 return

        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, returns))
            .Returns(new Dictionary<Asset, decimal> { [a] = 1.0m });

        var volTarget = new VolatilityTargetingConstruction(baseModel.Object, 0.10m);

        var weights = volTarget.ComputeTargetWeights(assets, returns);
        weights[a].Should().Be(1.0m);
    }

    [Fact]
    public void VolTarget_Constructor_InvalidTargetVol_ShouldThrow()
    {
        var baseModel = new Mock<IPortfolioConstructionModel>();
        var act = () => new VolatilityTargetingConstruction(baseModel.Object, -0.1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Regression: When the base model returns weights for only a subset of the input assets,
    /// VolatilityTargetingConstruction must throw instead of silently defaulting missing assets
    /// to 0 weight. The silent zero skewed portfolio vol estimation and scaling.
    /// </summary>
    [Fact]
    public void VolTarget_ShouldThrow_WhenBaseModelMissesAsset()
    {
        // Arrange — 2 input assets, but base model only returns weight for one
        var vti = new Asset("VTI");
        var tlt = new Asset("TLT");
        var assets = new List<Asset> { vti, tlt };

        var returns = new[]
        {
            new[] { 0.02m, -0.02m, 0.02m, -0.02m, 0.02m },
            new[] { -0.01m, 0.01m, -0.01m, 0.01m, -0.01m }
        };

        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, returns))
            .Returns(new Dictionary<Asset, decimal> { [vti] = 1.0m }); // Missing TLT

        var volTarget = new VolatilityTargetingConstruction(
            baseModel.Object, targetVolatility: 0.10m);

        // Act & Assert — should throw, not silently use 0 weight for TLT
        var act = () => volTarget.ComputeTargetWeights(assets, returns);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TLT*");
    }
}
