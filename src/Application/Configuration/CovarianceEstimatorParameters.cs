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
/// Declarative hyperparameters for the covariance estimator selected by
/// <see cref="BacktestOptions.CovarianceEstimator"/>. Binds to the
/// <c>Backtest:CovarianceParameters</c> subsection of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each property is <b>nullable</b> with "defer to the estimator's own constructor default"
/// semantics: when a property is <see langword="null"/> — the bind-time value when the
/// corresponding configuration key (or the whole <c>CovarianceParameters</c> section) is absent —
/// the DI factory constructs the estimator <i>without</i> supplying that argument, so the
/// estimator's documented default applies. The default for every parameter therefore lives in
/// exactly one place: the estimator's constructor.
/// </para>
/// <para>
/// A configured value flows directly to the estimator's constructor, so an out-of-range value is
/// rejected by that constructor's own validation (e.g. <see cref="System.ArgumentOutOfRangeException"/>)
/// at DI-resolution time, rather than being silently dropped.
/// </para>
/// <para>
/// Parameterless estimators (<c>Sample</c>, <c>LedoitWolf</c>, <c>LedoitWolfConstantCorrelation</c>,
/// <c>LedoitWolfSingleFactor</c>, <c>OAS</c>, <c>QIS</c>, <c>TracyWidomDenoised</c>) take no
/// hyperparameters and ignore every property here.
/// </para>
/// </remarks>
public sealed class CovarianceEstimatorParameters
{
    /// <summary>
    /// EWMA decay factor <c>lambda</c> for the <c>EWMA</c> estimator. Must lie in <c>(0, 1)</c>.
    /// <see langword="null"/> defers to the estimator default (<c>0.94</c>, RiskMetrics).
    /// </summary>
    public decimal? EwmaLambda { get; set; }

    /// <summary>
    /// Whether the <c>Denoised</c> estimator composes a light Ledoit-Wolf shrinkage after
    /// denoising. <see langword="null"/> defers to the estimator default (<c>false</c>).
    /// (The <c>DenoisedLW</c> convenience name forces <see langword="true"/> regardless.)
    /// </summary>
    public bool? DenoisedApplyLedoitWolfShrinkage { get; set; }

    /// <summary>
    /// Detoning intensity <c>detoningAlpha</c> for the <c>Detoned</c> estimator. Must lie in
    /// <c>[0, 1]</c> (<c>0</c> = no detoning, <c>1</c> = full PC1 removal). <see langword="null"/>
    /// defers to the estimator default (<c>1.0</c>).
    /// </summary>
    public decimal? DetoningAlpha { get; set; }

    /// <summary>
    /// Eigenvector sparsification threshold for the <c>DoublySparse</c> estimator.
    /// <see langword="null"/> defers to the estimator default (<c>0.1</c>).
    /// </summary>
    public decimal? DoublySparseEigenvectorThreshold { get; set; }

    /// <summary>
    /// Number of latent factors for the <c>FactorGraphicalLasso</c> estimator.
    /// <see langword="null"/> defers to the estimator default (<c>1</c>).
    /// </summary>
    public int? FactorGraphicalLassoNumFactors { get; set; }

    /// <summary>
    /// Gerber statistic threshold <c>c</c> (fraction of a standard deviation) for the
    /// <c>Gerber</c> estimator. Must be strictly positive. <see langword="null"/> defers to the
    /// estimator default (<c>0.5</c>).
    /// </summary>
    public decimal? GerberThreshold { get; set; }

    /// <summary>
    /// Split fraction for the <c>NERCOME</c> nonparametric split-sample estimator.
    /// <see langword="null"/> defers to the estimator default (<c>0.5</c>).
    /// </summary>
    public decimal? NercomeSplitFraction { get; set; }

    /// <summary>
    /// Number of latent factors for the <c>POET</c> estimator. <see langword="null"/> defers to
    /// the estimator default (<c>1</c>).
    /// </summary>
    public int? PoetNumFactors { get; set; }

    /// <summary>
    /// Soft-thresholding multiplier for the <c>POET</c> estimator's sparse residual.
    /// <see langword="null"/> defers to the estimator default (<c>0.5</c>).
    /// </summary>
    public double? PoetThresholdMultiplier { get; set; }

    /// <summary>
    /// DCC scalar <c>alpha</c> (news impact) for the <c>DccNl</c> estimator. When estimation is
    /// enabled it is the optimizer's initial guess; when disabled it is the fixed value. Must lie
    /// in <c>(0, 1)</c> with <c>alpha + beta &lt; 1</c>. <see langword="null"/> defers to the
    /// estimator default (<c>0.01</c>).
    /// </summary>
    public decimal? DccNlAlpha { get; set; }

    /// <summary>
    /// DCC scalar <c>beta</c> (persistence) for the <c>DccNl</c> estimator. When estimation is
    /// enabled it is the optimizer's initial guess; when disabled it is the fixed value. Must lie
    /// in <c>(0, 1)</c> with <c>alpha + beta &lt; 1</c>. <see langword="null"/> defers to the
    /// estimator default (<c>0.97</c>).
    /// </summary>
    public decimal? DccNlBeta { get; set; }

    /// <summary>
    /// Whether the <c>DccNl</c> estimator estimates <c>(alpha, beta)</c> by per-rebalance
    /// quasi-maximum-likelihood (the default behavior). Set <see langword="false"/> to hold
    /// <see cref="DccNlAlpha"/>/<see cref="DccNlBeta"/> fixed — the cheaper, deterministic V1
    /// behavior. <see langword="null"/> defers to the estimator default (<c>true</c>).
    /// </summary>
    public bool? DccNlEstimateParameters { get; set; }
}
