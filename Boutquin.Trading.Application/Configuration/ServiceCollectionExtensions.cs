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
using PortfolioConstruction;
using RiskManagement;
using SlippageModels;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        // Register construction models
        services.AddTransient<EqualWeightConstruction>();
        services.AddTransient<InverseVolatilityConstruction>();
        services.AddTransient<MinimumVarianceConstruction>();

        // Register covariance estimators
        services.AddTransient<SampleCovarianceEstimator>();

        // Register cost models (factory-based, configured via options)
        services.AddSingleton<ITransactionCostModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CostModelOptions>>().Value;
            return options.TransactionCostType switch
            {
                "PercentageOfValue" => new PercentageOfValueCostModel(options.CommissionRate),
                "PerShare" => new PerShareCostModel(options.CommissionRate),
                _ => new FixedPerTradeCostModel(options.CommissionRate),
            };
        });

        // Register slippage model
        services.AddSingleton<ISlippageModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CostModelOptions>>().Value;
            return options.SlippageType switch
            {
                "FixedSlippage" => new FixedSlippage(options.SlippageAmount),
                "PercentageSlippage" => new PercentageSlippage(options.SlippageAmount),
                _ => new NoSlippage(),
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

        // Register portfolio construction model (factory-based, configured via options)
        services.AddSingleton<IPortfolioConstructionModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BacktestOptions>>().Value;
            return options.ConstructionModel switch
            {
                "InverseVolatility" => new InverseVolatilityConstruction(),
                "MinimumVariance" => new MinimumVarianceConstruction(new SampleCovarianceEstimator()),
                "MeanVariance" => new MeanVarianceConstruction(new SampleCovarianceEstimator()),
                "RiskParity" => new RiskParityConstruction(new SampleCovarianceEstimator()),
                _ => new EqualWeightConstruction(),
            };
        });

        // Register backtest
        services.AddTransient<BackTest>();

        return services;
    }
}
