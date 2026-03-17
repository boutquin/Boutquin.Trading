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
/// Fetches factor return data from academic data libraries.
/// Returns all factors from a dataset in a single call as an async stream of
/// (date, factor-name-to-value dictionary) pairs in chronological order.
/// Values are in the source's native units (typically percentage returns).
/// </summary>
public interface IFactorDataFetcher
{
    /// <summary>
    /// Fetches daily factor returns for the specified dataset.
    /// </summary>
    /// <param name="dataset">The dataset to fetch (e.g., ThreeFactors, FiveFactors, Momentum).</param>
    /// <param name="startDate">Optional start date filter (inclusive).</param>
    /// <param name="endDate">Optional end date filter (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Async stream of (date, factors) pairs where factors is a dictionary mapping
    /// factor names (e.g., "Mkt-RF", "SMB") to their return values.
    /// Missing-data rows are skipped entirely.
    /// </returns>
    IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchDailyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches monthly factor returns for the specified dataset.
    /// </summary>
    /// <param name="dataset">The dataset to fetch (e.g., ThreeFactors, FiveFactors, Momentum).</param>
    /// <param name="startDate">Optional start date filter (inclusive). Day component is ignored; filters by year-month.</param>
    /// <param name="endDate">Optional end date filter (inclusive). Day component is ignored; filters by year-month.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Async stream of (date, factors) pairs. Monthly dates use the last calendar day of the month.
    /// Annual summary rows are excluded — only monthly observations are returned.
    /// </returns>
    IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchMonthlyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);
}
