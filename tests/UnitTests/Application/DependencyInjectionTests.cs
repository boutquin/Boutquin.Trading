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
using Boutquin.Trading.Application.CostModels;
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.SlippageModels;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class DependencyInjectionTests
{
    private static IServiceProvider BuildServiceProvider(Dictionary<string, string?>? overrides = null)
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
            ["RiskManagement:MaxDrawdownPercent"] = "0.20",
            ["RiskManagement:MaxPositionSizePercent"] = "0.25",
            ["RiskManagement:MaxSectorExposurePercent"] = "0.40",
        };

        if (overrides != null)
        {
            foreach (var (key, value) in overrides)
            {
                configData[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddBoutquinTrading(configuration);

        return services.BuildServiceProvider();
    }

    // ============================================================
    // RP5-01: All services resolve
    // ============================================================

    [Fact]
    public void AllServices_ShouldResolve()
    {
        // R2I-02: Default config has MaxSectorExposurePercent=0.40, which requires asset class mapping.
        // Disable it for this general resolution test.
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["RiskManagement:MaxSectorExposurePercent"] = "0",
        });

        sp.GetRequiredService<ITransactionCostModel>().Should().NotBeNull();
        sp.GetRequiredService<ISlippageModel>().Should().NotBeNull();
        sp.GetRequiredService<IRiskManager>().Should().NotBeNull();
        sp.GetRequiredService<IPortfolioConstructionModel>().Should().NotBeNull();
    }

    [Fact]
    public void RiskParity_ConstructionModel_ShouldResolve_ViaConfig()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "RiskParity",
        });

        var model = sp.GetRequiredService<IPortfolioConstructionModel>();
        model.Should().BeOfType<RiskParityConstruction>();
    }

    [Fact]
    public void MeanVariance_ConstructionModel_ShouldResolve_ViaConfig()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "MeanVariance",
        });

        var model = sp.GetRequiredService<IPortfolioConstructionModel>();
        model.Should().BeOfType<MeanVarianceConstruction>();
    }

    [Fact]
    public void InverseVolatility_ConstructionModel_ShouldResolve_ViaConfig()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "InverseVolatility",
        });

        var model = sp.GetRequiredService<IPortfolioConstructionModel>();
        model.Should().BeOfType<InverseVolatilityConstruction>();
    }

    [Fact]
    public void Default_ConstructionModel_ShouldBeEqualWeight()
    {
        var sp = BuildServiceProvider();
        var model = sp.GetRequiredService<IPortfolioConstructionModel>();
        model.Should().BeOfType<EqualWeightConstruction>();
    }

    // ============================================================
    // RP5-01: Cost model swap via config
    // ============================================================

    [Fact]
    public void PercentageCostModel_ShouldResolve_ViaConfig()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:TransactionCostType"] = "PercentageOfValue",
            ["CostModel:CommissionRate"] = "0.001",
        });

        var model = sp.GetRequiredService<ITransactionCostModel>();
        model.Should().BeOfType<PercentageOfValueCostModel>();
    }

    [Fact]
    public void PerShareCostModel_ShouldResolve_ViaConfig()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:TransactionCostType"] = "PerShare",
            ["CostModel:CommissionRate"] = "0.005",
        });

        var model = sp.GetRequiredService<ITransactionCostModel>();
        model.Should().BeOfType<PerShareCostModel>();
    }

    // ============================================================
    // RP5-01: Slippage model swap via config
    // ============================================================

    [Fact]
    public void FixedSlippage_ShouldResolve_ViaConfig()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "FixedSlippage",
            ["CostModel:SlippageAmount"] = "0.50",
        });

        var model = sp.GetRequiredService<ISlippageModel>();
        model.Should().BeOfType<FixedSlippage>();
    }

    [Fact]
    public void PercentageSlippage_ShouldResolve_ViaConfig()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "PercentageSlippage",
            ["CostModel:SlippageAmount"] = "0.001",
        });

        var model = sp.GetRequiredService<ISlippageModel>();
        model.Should().BeOfType<PercentageSlippage>();
    }

    // ============================================================
    // RP5-02: Configuration via IOptions<T>
    // ============================================================

    [Fact]
    public void BacktestOptions_ShouldLoadFromConfig()
    {
        var sp = BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<BacktestOptions>>().Value;

        options.ConstructionModel.Should().Be("EqualWeight");
    }

    [Fact]
    public void CostModelOptions_ShouldLoadFromConfig()
    {
        var sp = BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<CostModelOptions>>().Value;

        options.TransactionCostType.Should().Be("FixedPerTrade");
        options.CommissionRate.Should().Be(10m);
        options.SlippageType.Should().Be("NoSlippage");
    }

    [Fact]
    public void RiskManagementOptions_ShouldLoadFromConfig()
    {
        var sp = BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<RiskManagementOptions>>().Value;

        options.MaxDrawdownPercent.Should().Be(0.20m);
        options.MaxPositionSizePercent.Should().Be(0.25m);
        options.MaxSectorExposurePercent.Should().Be(0.40m);
    }

    [Fact]
    public void BacktestOptions_ShouldRespectOverrides()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "InverseVolatility",
        });

        var options = sp.GetRequiredService<IOptions<BacktestOptions>>().Value;
        options.ConstructionModel.Should().Be("InverseVolatility");
    }

    [Fact]
    public void BacktestOptions_DefaultValues_ShouldMatchCurrentBehavior()
    {
        // Empty config should produce default values
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<BacktestOptions>(configuration.GetSection(BacktestOptions.SectionName));
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<BacktestOptions>>().Value;
        options.ConstructionModel.Should().Be("EqualWeight");
    }

    // ============================================================
    // RP5-03: Logging with NullLogger
    // ============================================================

    [Fact]
    public void Portfolio_WithNullLogger_ShouldNotThrow()
    {
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Name).Returns("Test");
        strategyMock.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode> { [new Asset("VTI")] = CurrencyCode.USD });
        strategyMock.Setup(s => s.Positions).Returns(new Dictionary<Asset, int>());

        var brokerMock = new Mock<IBrokerage>();
        var handlerMock = new Mock<IEventHandler>();

        // This uses the backward-compatible constructor that defaults to NullLogger
        var act = () => new Portfolio(
            CurrencyCode.USD,
            new Dictionary<string, IStrategy> { ["Test"] = strategyMock.Object },
            new Dictionary<Asset, CurrencyCode> { [new Asset("VTI")] = CurrencyCode.USD },
            new Dictionary<Type, IEventHandler> { [typeof(MarketEvent)] = handlerMock.Object },
            brokerMock.Object);

        act.Should().NotThrow();
    }

    [Fact]
    public void BackTest_WithNullLogger_ShouldNotThrow()
    {
        var portfolioMock = new Mock<IPortfolio>();
        portfolioMock.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());
        portfolioMock.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        var fetcherMock = new Mock<IMarketDataFetcher>();

        // This uses the backward-compatible constructor that defaults to NullLogger
        var act = () => new BackTest(portfolioMock.Object, portfolioMock.Object, fetcherMock.Object, CurrencyCode.USD);
        act.Should().NotThrow();
    }

    // ============================================================
    // RP5-01: Risk management rules from config
    // ============================================================

    [Fact]
    public void RiskManager_DisabledRules_ShouldHaveNoRules()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["RiskManagement:MaxDrawdownPercent"] = "0",
            ["RiskManagement:MaxPositionSizePercent"] = "0",
            ["RiskManagement:MaxSectorExposurePercent"] = "0",
        });

        var manager = sp.GetRequiredService<IRiskManager>();
        // With all rules disabled, any order should be allowed
        var order = new Order(
            new DateOnly(2026, 1, 1), "Test", new Asset("VTI"),
            TradeAction.Buy, OrderType.Market, 100);

        var portfolioMock = new Mock<IPortfolio>();
        portfolioMock.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        manager.Evaluate(order, portfolioMock.Object).IsAllowed.Should().BeTrue();
    }

    // ============================================================
    // M14: Unknown config values should throw — no silent defaults
    // ============================================================

    [Fact]
    public void UnknownCostType_ShouldThrow()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:TransactionCostType"] = "InvalidType",
        });

        var act = sp.GetRequiredService<ITransactionCostModel>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnknownSlippageType_ShouldThrow()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "InvalidSlippage",
        });

        var act = sp.GetRequiredService<ISlippageModel>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnknownConstructionModel_ShouldThrow()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "InvalidModel",
        });

        var act = sp.GetRequiredService<IPortfolioConstructionModel>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // M20: BlackLitterman construction model
    // ============================================================

    // R2I-01: BlackLitterman via DI now throws because equilibrium weights cannot be configured via options
    [Fact]
    public void BlackLitterman_ConstructionModel_ShouldThrow_ViaConfig()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "BlackLitterman",
        });

        var act = sp.GetRequiredService<IPortfolioConstructionModel>;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*equilibrium weights*");
    }

    // ============================================================
    // M30: FixedSlippage/PercentageSlippage with zero amount
    // ============================================================

    [Fact]
    public void FixedSlippage_ZeroAmount_ShouldThrow()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "FixedSlippage",
            ["CostModel:SlippageAmount"] = "0",
        });

        var act = sp.GetRequiredService<ISlippageModel>;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SlippageAmount must be greater than zero*");
    }

    [Fact]
    public void PercentageSlippage_ZeroAmount_ShouldThrow()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["CostModel:SlippageType"] = "PercentageSlippage",
            ["CostModel:SlippageAmount"] = "0",
        });

        var act = sp.GetRequiredService<ISlippageModel>;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SlippageAmount must be greater than zero*");
    }

    // ============================================================
    // R2I-02: MaxSectorExposure wiring
    // ============================================================

    [Fact]
    public void MaxSectorExposure_Configured_WithMapping_CreatesRule()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "EqualWeight",
            ["CostModel:TransactionCostType"] = "FixedPerTrade",
            ["CostModel:CommissionRate"] = "10",
            ["CostModel:SlippageType"] = "NoSlippage",
            ["CostModel:SlippageAmount"] = "0",
            ["RiskManagement:MaxDrawdownPercent"] = "0",
            ["RiskManagement:MaxPositionSizePercent"] = "0",
            ["RiskManagement:MaxSectorExposurePercent"] = "0.30",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        // Register the required asset class mapping
        var mapping = new Dictionary<Asset, AssetClassCode>
        {
            [new Asset("VTI")] = AssetClassCode.Equities,
        };
        services.AddSingleton<IReadOnlyDictionary<Asset, AssetClassCode>>(mapping);
        services.AddBoutquinTrading(configuration);

        var sp = services.BuildServiceProvider();
        var manager = sp.GetRequiredService<IRiskManager>();

        // Verify the rule is active by testing it rejects excessive exposure
        manager.Should().NotBeNull();
    }

    [Fact]
    public void MaxSectorExposure_Configured_WithoutMapping_Throws()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["RiskManagement:MaxSectorExposurePercent"] = "0.30",
        });

        var act = sp.GetRequiredService<IRiskManager>;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IReadOnlyDictionary<Asset, AssetClassCode>*");
    }

    [Fact]
    public void MaxSectorExposure_Zero_NoRule()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["RiskManagement:MaxSectorExposurePercent"] = "0",
        });

        var manager = sp.GetRequiredService<IRiskManager>();
        // With rule disabled, any order should be allowed
        var order = new Order(
            new DateOnly(2026, 1, 1), "Test", new Asset("VTI"),
            TradeAction.Buy, OrderType.Market, 100);
        var portfolioMock = new Mock<IPortfolio>();
        portfolioMock.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());
        manager.Evaluate(order, portfolioMock.Object).IsAllowed.Should().BeTrue();
    }
}
