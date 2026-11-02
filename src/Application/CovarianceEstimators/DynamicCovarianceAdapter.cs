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

namespace Boutquin.Trading.Application.CovarianceEstimators;

/// <summary>
/// Adapts an <see cref="IDynamicCovarianceEstimator"/> (e.g. DCC-NL) to the static
/// <see cref="ICovarianceEstimator"/> interface consumed by every
/// <see cref="IPortfolioConstructionModel"/>. On each call to <see cref="Estimate"/>,
/// the adapter fits the full model to the supplied return window, then returns the
/// one-step-ahead forecast <c>H_{T+1|T}</c> — the theoretically correct covariance
/// for a portfolio rebalanced at <c>T+1</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the forecast, not the terminal in-sample matrix.</b> Construction models
/// optimize for the <i>next</i> rebalancing period. The last in-sample estimate
/// <c>H_T</c> describes covariance at the end of the lookback window; the one-step
/// forecast <c>H_{T+1|T}</c> is the model's best prediction of the covariance that
/// will prevail at the rebalancing date. For DCC-NL under the stationarity condition
/// α+β&lt;1 both are PSD; the forecast is preferred on theoretical grounds.
/// </para>
/// <para>
/// <b>Concurrency contract (inherited from the inner estimator).</b> The adapter is
/// single-threaded by contract: <see cref="Estimate"/> calls the inner estimator's
/// <see cref="IDynamicCovarianceEstimator.Fit"/>, which is a single-writer operation
/// that must run to completion before any read. Sequential rebalancing events (the
/// normal backtest cadence) satisfy this contract automatically. Concurrent calls to
/// <see cref="Estimate"/> from multiple threads violate this contract and produce
/// undefined behavior (data race on the inner estimator's mutable state); the caller
/// is responsible for ensuring sequential access.
/// </para>
/// </remarks>
public sealed class DynamicCovarianceAdapter : ICovarianceEstimator
{
    private readonly IDynamicCovarianceEstimator _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicCovarianceAdapter"/> class.
    /// </summary>
    /// <param name="inner">The dynamic estimator to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is <see langword="null"/>.</exception>
    public DynamicCovarianceAdapter(IDynamicCovarianceEstimator inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Fits the inner dynamic estimator to <paramref name="returns"/> and returns
    /// the one-step-ahead forecast covariance <c>H_{T+1|T}</c>.
    /// </remarks>
    public decimal[,] Estimate(decimal[][] returns)
    {
        _inner.Fit(returns);
        return _inner.Forecast();
    }
}
