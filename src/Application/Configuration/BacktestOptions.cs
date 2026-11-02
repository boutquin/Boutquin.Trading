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

/// <summary>
/// Configuration options for backtest execution.
/// Binds to the "Backtest" section of appsettings.json.
/// </summary>
/// <remarks>
/// Only properties that are consumed by the DI/runtime pipeline are included here.
/// Backtest parameters like StartDate, EndDate, BaseCurrency, RiskFreeRate, and
/// BurnInEndDate are passed directly to BackTest constructor/RunAsync — they are
/// not part of the DI options pattern because BackTest is not DI-registered.
/// </remarks>
public sealed class BacktestOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Backtest";

    /// <summary>
    /// The portfolio construction model to use.
    /// </summary>
    public string ConstructionModel { get; set; } = "EqualWeight";

    /// <summary>
    /// The covariance estimator to use for construction models that require one.
    /// Valid values: Sample, EWMA, LedoitWolf, LedoitWolfConstantCorrelation,
    /// LedoitWolfSingleFactor, OAS, QIS, Denoised, DenoisedLW, Gerber, FactorGraphicalLasso,
    /// DccNl, TracyWidomDenoised, Detoned, NERCOME, POET, DoublySparse.
    /// Defaults to "LedoitWolfConstantCorrelation" — a well-suited equity target.
    /// </summary>
    /// <remarks>
    /// Per-estimator hyperparameters (e.g. EWMA decay, DCC-NL <c>(alpha, beta)</c> and the
    /// <c>estimateParameters</c> opt-out) are configured declaratively via
    /// <see cref="CovarianceParameters"/>; an unset hyperparameter defers to the estimator's
    /// own constructor default.
    /// </remarks>
    public string CovarianceEstimator { get; set; } = "LedoitWolfConstantCorrelation";

    /// <summary>
    /// Declarative hyperparameters for the selected <see cref="CovarianceEstimator"/>, bound from
    /// the <c>Backtest:CovarianceParameters</c> subsection. Pre-initialized so it is never
    /// <see langword="null"/>; every inner property is itself nullable and defers to the
    /// estimator's constructor default when unset.
    /// </summary>
    public CovarianceEstimatorParameters CovarianceParameters { get; set; } = new();

    /// <summary>
    /// The expected-return estimator that return-aware construction models use as the mean input to
    /// their optimization objective. Valid values: <c>HistoricalMean</c> | <c>YieldAnchored</c>.
    /// Defaults to <c>"HistoricalMean"</c> — the behavior-preserving rear-view sample mean.
    /// </summary>
    /// <remarks>
    /// Bound from <c>Backtest:ExpectedReturnEstimator</c> and resolved to an
    /// <see cref="Boutquin.Trading.Domain.Interfaces.IExpectedReturnEstimator"/> by
    /// <see cref="ServiceCollectionExtensions.AddBoutquinTrading"/>, mirroring
    /// <see cref="CovarianceEstimator"/>'s factory pattern (a bad name throws at resolution — no silent
    /// default). <c>YieldAnchored</c> requires a per-run asset-class map and constructor-bound auxiliary
    /// series that configuration cannot express, so — like <c>BlackLitterman</c> — it cannot be built via
    /// DI: selecting it throws, directing the caller to construct
    /// <c>YieldAnchoredExpectedReturnEstimator</c> manually with the current universe's data.
    /// </remarks>
    public string ExpectedReturnEstimator { get; set; } = "HistoricalMean";
}
