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

using Caching;
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

            // R2I-02: Wire MaxSectorExposureRule when configured
            if (options.MaxSectorExposurePercent > 0)
            {
                var assetClassMapping = sp.GetService<IReadOnlyDictionary<Domain.ValueObjects.Asset, AssetClassCode>>()
                    ?? throw new InvalidOperationException(
                        "MaxSectorExposurePercent is configured but no IReadOnlyDictionary<Asset, AssetClassCode> " +
                        "is registered in DI. Register the asset-class mapping or set MaxSectorExposurePercent to 0.");
                rules.Add(new MaxSectorExposureRule(options.MaxSectorExposurePercent, assetClassMapping));
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
                // R2I-01: BlackLitterman requires non-empty equilibrium weights.
                // Registration via DI is not supported — register manually with proper weights.
                "BlackLitterman" => throw new InvalidOperationException(
                    "BlackLitterman construction model requires non-empty equilibrium weights. " +
                    "Configure equilibrium weights and register BlackLittermanConstruction manually " +
                    "instead of using AddBoutquinTrading DI registration."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.ConstructionModel),
                    options.ConstructionModel,
                    "Unknown ConstructionModel. Valid values: EqualWeight, InverseVolatility, MinimumVariance, MeanVariance, RiskParity, BlackLitterman."),
            };
        });

        // L8: Removed BackTest transient registration — it requires constructor params that DI cannot resolve.

        // Bind cache options and register decorators
        services.AddBoutquinTradingCaching(configuration);

        return services;
    }

    /// <summary>
    /// Registers caching decorators (L1 memory, L2 CSV write-through) around
    /// pre-registered <see cref="IMarketDataFetcher"/>, <see cref="IEconomicDataFetcher"/>,
    /// and <see cref="IFactorDataFetcher"/> instances.
    /// <para>
    /// Call this after the base fetchers are registered. If a base fetcher is not registered,
    /// the decorator for that fetcher type is skipped.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root (reads "Cache" section).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBoutquinTradingCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.AgainstNull(() => services);
        Guard.AgainstNull(() => configuration);

        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

        // Snapshot the current base registrations before replacing them
        var baseMarketData = FindDescriptor<IMarketDataFetcher>(services);
        var baseEconomic = FindDescriptor<IEconomicDataFetcher>(services);
        var baseFactor = FindDescriptor<IFactorDataFetcher>(services);

        if (baseMarketData != null)
        {
            services.Remove(baseMarketData);
            services.AddSingleton<IMarketDataFetcher>(sp =>
            {
                var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
                IMarketDataFetcher fetcher = ResolveFromDescriptor<IMarketDataFetcher>(baseMarketData, sp);

                if (cacheOptions.DataDirectory != null)
                {
                    fetcher = new WriteThroughMarketDataFetcher(fetcher, cacheOptions.DataDirectory);
                }

                if (cacheOptions.EnableMemoryCache)
                {
                    fetcher = new CachingMarketDataFetcher(fetcher);
                }

                return fetcher;
            });
        }

        if (baseEconomic != null)
        {
            services.Remove(baseEconomic);
            services.AddSingleton<IEconomicDataFetcher>(sp =>
            {
                var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
                IEconomicDataFetcher fetcher = ResolveFromDescriptor<IEconomicDataFetcher>(baseEconomic, sp);

                if (cacheOptions.DataDirectory != null)
                {
                    fetcher = new WriteThroughEconomicDataFetcher(fetcher, cacheOptions.DataDirectory);
                }

                if (cacheOptions.EnableMemoryCache)
                {
                    fetcher = new CachingEconomicDataFetcher(fetcher);
                }

                return fetcher;
            });
        }

        if (baseFactor != null)
        {
            services.Remove(baseFactor);
            services.AddSingleton<IFactorDataFetcher>(sp =>
            {
                var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
                IFactorDataFetcher fetcher = ResolveFromDescriptor<IFactorDataFetcher>(baseFactor, sp);

                if (cacheOptions.DataDirectory != null)
                {
                    fetcher = new WriteThroughFactorDataFetcher(fetcher, cacheOptions.DataDirectory);
                }

                if (cacheOptions.EnableMemoryCache)
                {
                    fetcher = new CachingFactorDataFetcher(fetcher);
                }

                return fetcher;
            });
        }

        return services;
    }

    private static ServiceDescriptor? FindDescriptor<T>(IServiceCollection services)
    {
        return services.FirstOrDefault(d => d.ServiceType == typeof(T));
    }

    private static T ResolveFromDescriptor<T>(ServiceDescriptor descriptor, IServiceProvider sp)
        where T : class
    {
        if (descriptor.ImplementationInstance is T instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory != null)
        {
            return (T)descriptor.ImplementationFactory(sp);
        }

        if (descriptor.ImplementationType != null)
        {
            return (T)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Cannot resolve base {typeof(T).Name} from the existing service descriptor.");
    }
}
