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

using Boutquin.Trading.Application.Configuration;
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.SlippageModels;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Exhaustive DI resolution tests for every registered construction model name,
/// cost model type, slippage type, and calendar type.
/// Ensures no typo or missing case in the ServiceCollectionExtensions switch expressions.
/// </summary>
public sealed class ExhaustiveDiRegistrationTests
{
    private static IServiceProvider BuildServiceProvider(Dictionary<string, string?> overrides)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Backtest:StartDate"] = "2020-01-01",
            ["Backtest:EndDate"] = "2025-12-31",
            ["Backtest:BaseCurrency"] = "USD",
            ["Backtest:RebalancingFrequency"] = "Monthly",
            ["Backtest:ConstructionModel"] = "EqualWeight",
            ["CostModel:TransactionCostType"] = "FixedPerTrade",
            ["CostModel:CommissionRate"] = "10",
            ["CostModel:SlippageType"] = "NoSlippage",
            ["CostModel:SlippageAmount"] = "0",
            ["CostModel:DefaultSpreadPercent"] = "0",
            ["CostModel:VolumeLimit"] = "0",
            ["RiskManagement:MaxDrawdownPercent"] = "0.20",
            ["RiskManagement:MaxPositionSizePercent"] = "0.25",
            ["RiskManagement:MaxSectorExposurePercent"] = "0",
        };

        foreach (var (key, value) in overrides)
        {
            configData[key] = value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddBoutquinTrading(configuration);

        return services.BuildServiceProvider();
    }

    // ============================================================
    // ALL construction model names
    // ============================================================

    [Theory]
    [InlineData("EqualWeight", typeof(EqualWeightConstruction))]
    [InlineData("InverseVolatility", typeof(InverseVolatilityConstruction))]
    [InlineData("MinimumVariance", typeof(MinimumVarianceConstruction))]
    [InlineData("MeanVariance", typeof(MeanVarianceConstruction))]
    [InlineData("RiskParity", typeof(RiskParityConstruction))]
    [InlineData("MaximumDiversification", typeof(MaximumDiversificationConstruction))]
    [InlineData("HierarchicalRiskParity", typeof(HierarchicalRiskParityConstruction))]
    [InlineData("ReturnTiltedHRP", typeof(ReturnTiltedHrpConstruction))]
    [InlineData("MeanCVaR", typeof(MeanDownsideRiskConstruction))]
    [InlineData("MeanSortino", typeof(MeanDownsideRiskConstruction))]
    [InlineData("RobustMeanVariance", typeof(RobustMeanVarianceConstruction))]
    [InlineData("HERC", typeof(HierarchicalEqualRiskContributionConstruction))]
    [InlineData("MeanCDaR", typeof(MeanDownsideRiskConstruction))]
    public void ConstructionModel_ShouldResolve(string modelName, Type expectedType)
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = modelName,
        });

        var model = sp.GetRequiredService<IPortfolioConstructionModel>();
        model.Should().BeOfType(expectedType);
    }

    [Fact]
    public void ConstructionModel_BlackLitterman_ShouldThrowInvalidOperation()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "BlackLitterman",
        });

        var act = sp.GetRequiredService<IPortfolioConstructionModel>;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*equilibrium weights*");
    }

    [Fact]
    public void ConstructionModel_Unknown_ShouldThrowArgumentOutOfRange()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "NonExistentModel",
        });

        var act = sp.GetRequiredService<IPortfolioConstructionModel>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // ALL cost model types
    // ============================================================

    [Theory]
    [InlineData("FixedPerTrade")]
    [InlineData("PercentageOfValue")]
    [InlineData("PerShare")]
    public void CostModel_ShouldResolve(string costType)
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:TransactionCostType"] = costType,
            ["CostModel:CommissionRate"] = "10",
        });

        var model = sp.GetRequiredService<ITransactionCostModel>();
        model.Should().NotBeNull();
    }

    [Fact]
    public void CostModel_Unknown_ShouldThrow()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:TransactionCostType"] = "InvalidCostType",
        });

        var act = sp.GetRequiredService<ITransactionCostModel>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // ALL slippage types
    // ============================================================

    [Fact]
    public void SlippageModel_NoSlippage_ShouldResolve()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "NoSlippage",
        });

        sp.GetRequiredService<ISlippageModel>().Should().BeOfType<NoSlippage>();
    }

    [Fact]
    public void SlippageModel_FixedSlippage_ShouldResolve()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "FixedSlippage",
            ["CostModel:SlippageAmount"] = "0.05",
        });

        sp.GetRequiredService<ISlippageModel>().Should().BeOfType<FixedSlippage>();
    }

    [Fact]
    public void SlippageModel_PercentageSlippage_ShouldResolve()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "PercentageSlippage",
            ["CostModel:SlippageAmount"] = "0.001",
        });

        sp.GetRequiredService<ISlippageModel>().Should().BeOfType<PercentageSlippage>();
    }

    [Fact]
    public void SlippageModel_VolumeShareSlippage_ShouldResolve()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "VolumeShareSlippage",
            ["CostModel:SlippageAmount"] = "0.1",
            ["CostModel:VolumeLimit"] = "0.025",
        });

        sp.GetRequiredService<ISlippageModel>().Should().BeOfType<VolumeShareSlippage>();
    }

    [Fact]
    public void SlippageModel_Unknown_ShouldThrow()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "InvalidSlippage",
        });

        var act = sp.GetRequiredService<ISlippageModel>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SlippageModel_FixedSlippageZeroAmount_ShouldThrow()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "FixedSlippage",
            ["CostModel:SlippageAmount"] = "0",
        });

        var act = sp.GetRequiredService<ISlippageModel>;
        act.Should().Throw<InvalidOperationException>();
    }
}
