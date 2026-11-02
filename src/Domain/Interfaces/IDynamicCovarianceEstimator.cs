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

namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// Estimates a <b>time-varying</b> (conditional) covariance matrix from a panel of
/// asset return series — one <c>N×N</c> matrix per observation date. This is the
/// dynamic counterpart of <see cref="ICovarianceEstimator"/> (which produces a single
/// static matrix): instead of one estimate for the whole sample, a dynamic estimator
/// tracks how the covariance evolves through volatility clusters and shifting
/// correlations (e.g. a DCC / DCC-NL model).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle.</b> <see cref="Fit"/> must be called first; it estimates the model
/// parameters and the full per-date conditional-covariance path. After a successful
/// <see cref="Fit"/>, <see cref="GetConditionalCovariances"/> and <see cref="Forecast"/>
/// return the fitted results. Calling either reader before <see cref="Fit"/> throws
/// <see cref="System.InvalidOperationException"/>.
/// </para>
/// <para>
/// <b>Concurrency contract (V1).</b> <b>Single-threaded by contract.</b>
/// <see cref="Fit"/> is a single-writer that must run to completion before any reader is
/// called; it is <i>not</i> thread-safe during execution and <i>not</i> reentrant. Once a
/// <see cref="Fit"/> has completed, the fitted state is immutable until the next
/// <see cref="Fit"/>, so concurrent reads (<see cref="GetConditionalCovariances"/> /
/// <see cref="Forecast"/>) after a completed <see cref="Fit"/> are safe.
/// </para>
/// </remarks>
public interface IDynamicCovarianceEstimator
{
    /// <summary>
    /// Fits the dynamic covariance model to the historical return panel. After this call
    /// completes, <see cref="GetConditionalCovariances"/> and <see cref="Forecast"/> are
    /// available.
    /// </summary>
    /// <param name="returns">
    /// Jagged return panel indexed <c>returns[asset][time]</c>: one inner array per asset,
    /// each holding that asset's return series in chronological order. All inner arrays must
    /// have the same length.
    /// </param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="returns"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when there are fewer than two assets, fewer than the minimum number of
    /// observations, or the asset series have mismatched lengths.
    /// </exception>
    void Fit(decimal[][] returns);

    /// <summary>
    /// Returns the per-date conditional covariance matrices <c>H_t</c> in chronological
    /// order. Each entry is an <c>N×N</c> matrix for one observation date; a leading warmup
    /// stretch may be excluded.
    /// </summary>
    /// <returns>An ordered, read-only list of conditional covariance matrices.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when called before a successful <see cref="Fit"/>.</exception>
    IReadOnlyList<decimal[,]> GetConditionalCovariances();

    /// <summary>
    /// Produces the one-step-ahead conditional covariance forecast from the last fitted
    /// date.
    /// </summary>
    /// <returns>The <c>N×N</c> forecast covariance matrix for the next date.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when called before a successful <see cref="Fit"/>.</exception>
    decimal[,] Forecast();
}
