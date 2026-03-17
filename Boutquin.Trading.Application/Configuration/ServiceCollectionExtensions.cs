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

namespace Boutquin.Trading.Application.Configuration;

using CostModels;
using CovarianceEstimators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PortfolioConstruction;
using RiskManagement;
using SlippageModels;

/// <summary>
/// Extension methods for registering Boutquin.Trading services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Boutquin.Trading services, options, and logging.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBoutquinTrading(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.AgainstNull(() => services);
        Guard.AgainstNull(() => configuration);

        // Bind configuration sections
        services.Configure<BacktestOptions>(configuration.GetSection(BacktestOptions.SectionName));
        services.Configure<CostModelOptions>(configuration.GetSection(CostModelOptions.SectionName));
        services.Configure<RiskManagementOptions>(configuration.GetSection(RiskManagementOptions.SectionName));

        // M28: Removed unused transient registrations for construction models and covariance estimator.
        // The factory below creates instances via `new`, so these registrations were never used.

        // M14: Register cost models with explicit cases — no silent defaults
        services.AddSingleton<ITransactionCostModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CostModelOptions>>().Value;
            return options.TransactionCostType switch
            {
                "FixedPerTrade" => new FixedPerTradeCostModel(options.CommissionRate),
                "PercentageOfValue" => new PercentageOfValueCostModel(options.CommissionRate),
                "PerShare" => new PerShareCostModel(options.CommissionRate),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.TransactionCostType),
                    options.TransactionCostType,
                    "Unknown TransactionCostType. Valid values: FixedPerTrade, PercentageOfValue, PerShare."),
            };
        });

        // M14/M30: Register slippage model with explicit cases and zero-amount validation
        services.AddSingleton<ISlippageModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CostModelOptions>>().Value;
            return options.SlippageType switch
            {
                "NoSlippage" => new NoSlippage(),
                "FixedSlippage" => options.SlippageAmount > 0
                    ? new FixedSlippage(options.SlippageAmount)
                    : throw new InvalidOperationException(
                        $"CostModel:SlippageAmount must be greater than zero when SlippageType is '{options.SlippageType}'."),
                "PercentageSlippage" => options.SlippageAmount > 0
                    ? new PercentageSlippage(options.SlippageAmount)
                    : throw new InvalidOperationException(
                        $"CostModel:SlippageAmount must be greater than zero when SlippageType is '{options.SlippageType}'."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.SlippageType),
                    options.SlippageType,
                    "Unknown SlippageType. Valid values: NoSlippage, FixedSlippage, PercentageSlippage."),
            };
        });

        // Register risk management
        services.AddSingleton<IRiskManager>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RiskManagementOptions>>().Value;
            var rules = new List<IRiskRule>();

            if (options.MaxDrawdownPercent > 0)
            {
                rules.Add(new MaxDrawdownRule(options.MaxDrawdownPercent));
            }

            if (options.MaxPositionSizePercent > 0)
            {
                rules.Add(new MaxPositionSizeRule(options.MaxPositionSizePercent));
            }

            return new RiskManager(rules);
        });

        // M14/M20: Register portfolio construction model with explicit cases — no silent defaults
        services.AddSingleton<IPortfolioConstructionModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BacktestOptions>>().Value;
            return options.ConstructionModel switch
            {
                "EqualWeight" => new EqualWeightConstruction(),
                "InverseVolatility" => new InverseVolatilityConstruction(),
                "MinimumVariance" => new MinimumVarianceConstruction(new SampleCovarianceEstimator()),
                "MeanVariance" => new MeanVarianceConstruction(new SampleCovarianceEstimator()),
                "RiskParity" => new RiskParityConstruction(new SampleCovarianceEstimator()),
                // BlackLitterman requires equilibrium weights — default to equal-weight placeholder.
                // Callers should configure proper equilibrium weights via the constructor directly.
                "BlackLitterman" => new BlackLittermanConstruction(
                    equilibriumWeights: [],
                    covarianceEstimator: new SampleCovarianceEstimator()),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.ConstructionModel),
                    options.ConstructionModel,
                    "Unknown ConstructionModel. Valid values: EqualWeight, InverseVolatility, MinimumVariance, MeanVariance, RiskParity, BlackLitterman."),
            };
        });

        // L8: Removed BackTest transient registration — it requires constructor params that DI cannot resolve.

        return services;
    }
}
