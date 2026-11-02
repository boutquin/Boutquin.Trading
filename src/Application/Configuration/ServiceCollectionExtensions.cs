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

using Boutquin.Trading.Recipes;
using CostModels;
using CovarianceEstimators;
using DownsideRisk;
using ExpectedReturns;
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
                "VolumeShareSlippage" => new VolumeShareSlippage(
                    options.VolumeLimit > 0 ? options.VolumeLimit : 0.025m,
                    options.SlippageAmount > 0 ? options.SlippageAmount : 0.1m),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.SlippageType),
                    options.SlippageType,
                    "Unknown SlippageType. Valid values: NoSlippage, FixedSlippage, PercentageSlippage, VolumeShareSlippage."),
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
                var assetClassMapping = sp.GetService<IReadOnlyDictionary<Symbol, AssetClassCode>>()
                    ?? throw new InvalidOperationException(
                        "MaxSectorExposurePercent is configured but no IReadOnlyDictionary<Symbol, AssetClassCode> " +
                        "is registered in DI. Register the asset-class mapping or set MaxSectorExposurePercent to 0.");
                rules.Add(new MaxSectorExposureRule(options.MaxSectorExposurePercent, assetClassMapping));
            }

            return new RiskManager(rules);
        });

        // Register the covariance estimator as a first-class DI service built from the selected
        // estimator name + its declarative hyperparameters (BacktestOptions.CovarianceParameters).
        // The construction-model factory below resolves it; exposing it as ICovarianceEstimator
        // also makes the wiring directly resolvable (and testable) on its own.
        services.AddSingleton<ICovarianceEstimator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BacktestOptions>>().Value;
            return CreateCovarianceEstimator(options.CovarianceEstimator, options.CovarianceParameters);
        });

        // M-9: Register the expected-return estimator from the selected name, mirroring the covariance
        // estimator's factory pattern (explicit cases, no silent default, throw on a bad name). Exposed
        // as IExpectedReturnEstimator so a caller can resolve it directly to feed a return-aware
        // construction model built outside the DI construction-model switch.
        services.AddSingleton<IExpectedReturnEstimator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BacktestOptions>>().Value;
            return options.ExpectedReturnEstimator switch
            {
                // Behavior-preserving rear-view sample mean — the default that keeps existing blueprints
                // unchanged (M-7 byte-identity baseline).
                "HistoricalMean" => new HistoricalMeanExpectedReturnEstimator(),
                // YieldAnchored needs a per-run per-symbol asset-class map and constructor-bound auxiliary
                // series (current yields / macro inputs) that configuration cannot express — the same
                // situation as BlackLitterman's equilibrium weights. Producing a DI instance would yield a
                // broken estimator, so require manual construction instead of silently mis-building one.
                "YieldAnchored" => throw new InvalidOperationException(
                    "YieldAnchored expected-return estimator requires a per-symbol asset-class map and " +
                    "constructor-bound auxiliary series (current yields / macro inputs) that configuration " +
                    "cannot express. Construct YieldAnchoredExpectedReturnEstimator manually with the " +
                    "current universe's data instead of using AddBoutquinTrading DI registration."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.ExpectedReturnEstimator),
                    options.ExpectedReturnEstimator,
                    "Unknown ExpectedReturnEstimator. Valid values: HistoricalMean, YieldAnchored."),
            };
        });

        // M14/M20: Register portfolio construction model with explicit cases — no silent defaults
        services.AddSingleton<IPortfolioConstructionModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BacktestOptions>>().Value;
            // Eager resolution (same point as the prior inline CreateCovarianceEstimator call), so a
            // bad estimator name / hyperparameter still throws when any construction model resolves.
            var cov = sp.GetRequiredService<ICovarianceEstimator>();
            return options.ConstructionModel switch
            {
                "EqualWeight" => new EqualWeightConstruction(),
                "InverseVolatility" => new InverseVolatilityConstruction(),
                "MinimumVariance" => new MinimumVarianceConstruction(cov),
                "MeanVariance" => new MeanVarianceConstruction(cov),
                "RiskParity" => new RiskParityConstruction(cov),
                "MaximumDiversification" => new MaximumDiversificationConstruction(cov),
                "HierarchicalRiskParity" => new HierarchicalRiskParityConstruction(cov),
                "ReturnTiltedHRP" => new ReturnTiltedHrpConstruction(cov),
                "MeanCVaR" => new MeanDownsideRiskConstruction(new CVaRRiskMeasure()),
                "MeanSortino" => new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure()),
                "RobustMeanVariance" => new RobustMeanVarianceConstruction(cov),
                "HERC" => new HierarchicalEqualRiskContributionConstruction(cov),
                "MeanCDaR" => new MeanDownsideRiskConstruction(new CDaRRiskMeasure()),
                // M-7: Mean − λ·EVaR (Ahmadi-Javid 2012 Entropic Value-at-Risk). EVaR is the
                // Chernoff-bound upper envelope of CVaR (VaR ≤ CVaR ≤ EVaR), so MeanEVaR is the
                // most conservative of the downside-risk construction trio. Default α=0.95.
                "MeanEVaR" => new MeanDownsideRiskConstruction(new EntropicValueAtRiskMeasure()),
                // M-4: Entropy Pooling (Fully Flexible Views). The DI-registered instance is the
                // no-views form (posterior == prior → mean-variance on the sample moments); views
                // require a pick matrix / view returns that configuration cannot express, so a
                // view-bearing EntropyPooling must be registered manually (as with BlackLitterman).
                "EntropyPooling" => new EntropyPoolingConstruction(),
                // M-5: Nested Clustered Optimization (López de Prado 2019). Robust HRP successor —
                // clusters the correlation matrix, optimizes intra-cluster then inter-cluster
                // min-var, and recombines. Consumes the injected covariance estimator.
                "NestedClusteredOptimization" => new NestedClusteredOptimizationConstruction(cov),
                "NCO" => new NestedClusteredOptimizationConstruction(cov),
                // M-6: Sparse (ℓ₁-regularized) minimum variance — Husmann, Shivarova &
                // Steinert (2019) Model 2 (LASSO). The DI-registered instance is the long-only
                // corner (δ=1); a non-binding budget (δ=∞) reduces to standard min-var. A
                // different ℓ₁ budget requires the δ parameter that configuration cannot
                // express, so a budgeted SparseMinVar must be registered manually.
                "SparseMinimumVariance" => new SparseMinimumVarianceConstruction(cov),
                // R2I-01: BlackLitterman requires non-empty equilibrium weights.
                // Registration via DI is not supported — register manually with proper weights.
                "BlackLitterman" => throw new InvalidOperationException(
                    "BlackLitterman construction model requires non-empty equilibrium weights. " +
                    "Configure equilibrium weights and register BlackLittermanConstruction manually " +
                    "instead of using AddBoutquinTrading DI registration."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.ConstructionModel),
                    options.ConstructionModel,
                    "Unknown ConstructionModel. Valid values: EqualWeight, InverseVolatility, MinimumVariance, MeanVariance, RiskParity, MaximumDiversification, HierarchicalRiskParity, ReturnTiltedHRP, MeanCVaR, MeanSortino, RobustMeanVariance, HERC, MeanCDaR, MeanEVaR, EntropyPooling, NestedClusteredOptimization, NCO, SparseMinimumVariance, BlackLitterman."),
            };
        });

        // L8: Removed BackTest transient registration — it requires constructor params that DI cannot resolve.

        // Register trading calendar
        services.Configure<CalendarOptions>(configuration.GetSection(CalendarOptions.SectionName));
        services.AddSingleton<IBusinessCalendar>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CalendarOptions>>().Value;
            return options.TradingCalendar.Trim() switch
            {
                var t when t.Equals("US", StringComparison.OrdinalIgnoreCase) => new UsNyseCalendar(),
                var t when t.Equals("Canadian", StringComparison.OrdinalIgnoreCase) => new CaTsxCalendar(),
                var t when t.Equals("Composite", StringComparison.OrdinalIgnoreCase) => CreateCompositeCalendar(options),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.TradingCalendar),
                    options.TradingCalendar,
                    "Unknown TradingCalendar. Valid values: US, Canadian, Composite."),
            };
        });

        // Register recipe layer — BacktestDatasetBuilder materializes IBacktestDataset via IDataPipeline
        services.AddSingleton<BacktestDatasetBuilder>();

        return services;
    }

    // Builds the selected covariance estimator, honouring the declarative hyperparameters in
    // <paramref name="p"/>. A null hyperparameter defers to the estimator's own constructor default
    // (the single source of truth for each default), so a configuration that omits a parameter — or
    // the whole CovarianceParameters section — reproduces the pre-spec defaults exactly. Out-of-range
    // values flow to the estimator constructor and surface as its own validation exception here.
    private static ICovarianceEstimator CreateCovarianceEstimator(
        string name, CovarianceEstimatorParameters p) =>
        name switch
        {
            "Sample" => new SampleCovarianceEstimator(),
            "EWMA" => p.EwmaLambda is { } lambda
                ? new ExponentiallyWeightedCovarianceEstimator(lambda)
                : new ExponentiallyWeightedCovarianceEstimator(),
            "LedoitWolf" => new LedoitWolfShrinkageEstimator(),
            "LedoitWolfConstantCorrelation" => new LedoitWolfConstantCorrelationEstimator(),
            "LedoitWolfSingleFactor" => new LedoitWolfSingleFactorEstimator(),
            "OAS" => new OracleApproximatingShrinkageEstimator(),
            "QIS" => new QuadraticInverseShrinkageEstimator(),
            "Denoised" => new DenoisedCovarianceEstimator(p.DenoisedApplyLedoitWolfShrinkage ?? false),
            // DenoisedLW is a convenience alias that forces shrinkage on regardless of the flag.
            "DenoisedLW" => new DenoisedCovarianceEstimator(applyLedoitWolfShrinkage: true),
            "Gerber" => p.GerberThreshold is { } c
                ? new GerberCovarianceEstimator(c)
                : new GerberCovarianceEstimator(),
            "FactorGraphicalLasso" => p.FactorGraphicalLassoNumFactors is { } fglFactors
                ? new FactorGraphicalLassoEstimator(fglFactors)
                : new FactorGraphicalLassoEstimator(),
            "DccNl" => new DynamicCovarianceAdapter(BuildDccNl(p)),
            "TracyWidomDenoised" => new TracyWidomDenoisedCovarianceEstimator(),
            "Detoned" => p.DetoningAlpha is { } detoningAlpha
                ? new DetonedCovarianceEstimator(detoningAlpha)
                : new DetonedCovarianceEstimator(),
            "NERCOME" => p.NercomeSplitFraction is { } splitFraction
                ? new NercomeCovarianceEstimator(splitFraction)
                : new NercomeCovarianceEstimator(),
            "POET" => new PoetCovarianceEstimator(p.PoetNumFactors ?? 1, p.PoetThresholdMultiplier ?? 0.5),
            "DoublySparse" => p.DoublySparseEigenvectorThreshold is { } eigenvectorThreshold
                ? new DoublySparseEstimator(eigenvectorThreshold)
                : new DoublySparseEstimator(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(name),
                name,
                "Unknown CovarianceEstimator. Valid values: Sample, EWMA, LedoitWolf, " +
                "LedoitWolfConstantCorrelation, LedoitWolfSingleFactor, OAS, QIS, " +
                "Denoised, DenoisedLW, Gerber, FactorGraphicalLasso, DccNl, TracyWidomDenoised, Detoned, NERCOME, POET, DoublySparse."),
        };

    // Defaults mirror DccNlCovarianceEstimator's constructor defaults (alpha=0.01, beta=0.97,
    // estimateParameters=true). A null hyperparameter therefore reproduces the pre-spec hard-coded
    // `new DccNlCovarianceEstimator(estimateParameters: true)` behavior exactly. Keep these three
    // literals in sync with the estimator constructor if its defaults ever change.
    private static DccNlCovarianceEstimator BuildDccNl(CovarianceEstimatorParameters p) =>
        new(p.DccNlAlpha ?? 0.01m, p.DccNlBeta ?? 0.97m, p.DccNlEstimateParameters ?? true);

    private static CompositeCalendar CreateCompositeCalendar(CalendarOptions options)
    {
        if (options.Calendars.Count == 0)
        {
            throw new ArgumentException(
                "Composite trading calendar requires at least one constituent calendar in CalendarOptions.Calendars.",
                nameof(options));
        }

        var compositionMode = options.CompositionMode.Trim() switch
        {
            var m when m.Equals("Any", StringComparison.OrdinalIgnoreCase) => CalendarCompositionMode.Any,
            var m when m.Equals("All", StringComparison.OrdinalIgnoreCase) => CalendarCompositionMode.All,
            _ => throw new ArgumentOutOfRangeException(
                nameof(options.CompositionMode),
                options.CompositionMode,
                "Unknown CompositionMode. Valid values: Any, All."),
        };

        var calendars = options.Calendars.Select<string, IBusinessCalendar>(name => name.Trim() switch
        {
            var n when n.Equals("US", StringComparison.OrdinalIgnoreCase) => new UsNyseCalendar(),
            var n when n.Equals("Canadian", StringComparison.OrdinalIgnoreCase) => new CaTsxCalendar(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(options.Calendars),
                name,
                "Unknown calendar in Calendars list. Valid values: US, Canadian."),
        }).ToArray();

        var code = string.Join("+", calendars.Select(c => c.Code));
        return new CompositeCalendar(code, compositionMode, calendars);
    }

}
