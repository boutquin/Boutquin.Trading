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

public sealed class VolatilityTargetingConstructionContractTests
{
    [Theory]
    [MemberData(nameof(VolatilityTargetingConstructionContractTestData.ScaleUpCases),
        MemberType = typeof(VolatilityTargetingConstructionContractTestData))]
    public void ComputeTargetWeights_WithScaleFactor_WeightsSumMayExceedOne(
        List<Asset> assets, decimal[][] returns, decimal targetVol, decimal maxLeverage)
    {
        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, It.IsAny<decimal[][]>()))
            .Returns(new Dictionary<Asset, decimal> { [assets[0]] = 1.0m });

        var model = new VolatilityTargetingConstruction(
            baseModel.Object, targetVol, maxLeverage);

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeGreaterThan(1.0m,
            "volatility targeting with targetVol > realizedVol should produce leveraged weights");
    }

    [Theory]
    [MemberData(nameof(VolatilityTargetingConstructionContractTestData.ScaleDownCases),
        MemberType = typeof(VolatilityTargetingConstructionContractTestData))]
    public void ComputeTargetWeights_WithScaleDown_WeightsSumLessThanOne(
        List<Asset> assets, decimal[][] returns, decimal targetVol, decimal maxLeverage)
    {
        var baseModel = new Mock<IPortfolioConstructionModel>();
        baseModel.Setup(m => m.ComputeTargetWeights(assets, It.IsAny<decimal[][]>()))
            .Returns(new Dictionary<Asset, decimal> { [assets[0]] = 1.0m });

        var model = new VolatilityTargetingConstruction(
            baseModel.Object, targetVol, maxLeverage);

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeLessThan(1.0m,
            "volatility targeting with targetVol < realizedVol should scale weights down");
    }

    [Fact]
    public void ComputeTargetWeights_ImplementsILeveragedConstructionModel()
    {
        typeof(VolatilityTargetingConstruction).GetInterfaces()
            .Should().Contain(t => t.Name == "ILeveragedConstructionModel",
                "VolatilityTargetingConstruction should implement ILeveragedConstructionModel");
    }
}
