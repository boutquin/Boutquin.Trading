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
/// Estimates the covariance matrix from a matrix of asset return series.
/// </summary>
public interface ICovarianceEstimator
{
    /// <summary>
    /// Estimates the covariance matrix for the given return series.
    /// </summary>
    /// <param name="returns">
    /// A jagged array where <c>returns[i]</c> is the return series for asset <c>i</c>.
    /// All series must have the same length and contain at least two observations.
    /// </param>
    /// <returns>
    /// A symmetric NxN covariance matrix where N is the number of assets.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the return series have different lengths or fewer than two observations.</exception>
    decimal[,] Estimate(decimal[][] returns);
}
