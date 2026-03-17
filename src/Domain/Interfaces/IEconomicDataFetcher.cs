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
/// Fetches scalar economic time series data (e.g., treasury yields, inflation indicators, GDP).
/// Returns raw values as the source provides them — caller is responsible for unit transformations.
/// </summary>
public interface IEconomicDataFetcher
{
    /// <summary>
    /// Fetches observations for an economic data series.
    /// Returns an async stream of (date, value) pairs in chronological order.
    /// Missing observations are silently skipped.
    /// </summary>
    /// <param name="seriesId">The series identifier (e.g., "DGS3MO" for 3-month treasury yield).</param>
    /// <param name="startDate">Optional start date filter (inclusive).</param>
    /// <param name="endDate">Optional end date filter (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async stream of date-value pairs. Missing observations are skipped.</returns>
    IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> FetchSeriesAsync(
        string seriesId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);
}
