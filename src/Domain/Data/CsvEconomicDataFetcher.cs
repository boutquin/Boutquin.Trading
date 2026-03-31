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

using System.Runtime.CompilerServices;

namespace Boutquin.Trading.Domain.Data;

/// <summary>
/// Fetches economic data series from CSV files.
/// CSV format: Date,Value (one series per file, named fred_{seriesId}.csv).
/// </summary>
public sealed class CsvEconomicDataFetcher : IEconomicDataFetcher
{
    private readonly string _dataDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvEconomicDataFetcher"/> class.
    /// </summary>
    /// <param name="directory">Path to the directory containing FRED CSV files.</param>
    public CsvEconomicDataFetcher(string directory)
    {
        _dataDirectory = directory ?? throw new ArgumentNullException(nameof(directory));

        if (!Directory.Exists(_dataDirectory))
        {
            try
            {
                Directory.CreateDirectory(_dataDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new MarketDataStorageException(
                    $"Failed to create data directory '{_dataDirectory}'.", ex);
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> FetchSeriesAsync(
        string seriesId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(() => seriesId);

        var fileName = GetCsvFileName(seriesId);

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"Economic data file not found for series {seriesId}.", fileName);
        }

#pragma warning disable CA2007
        await using var fileStream = File.OpenRead(fileName);
#pragma warning restore CA2007
        using var streamReader = new StreamReader(fileStream);

        // Skip the header row
        await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        while (await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var columns = line.Split(',');

            if (columns.Length < 2)
            {
                continue; // Skip malformed rows (missing columns)
            }

            if (!DateOnly.TryParse(columns[0], System.Globalization.CultureInfo.InvariantCulture, out var date))
            {
                continue; // Skip rows with unparseable dates (e.g., FRED "." missing values)
            }

            if (!decimal.TryParse(columns[1], System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                continue; // Skip rows with unparseable values (e.g., FRED "." missing values)
            }

            // Apply date filters
            if (startDate.HasValue && date < startDate.Value)
            {
                continue;
            }
            if (endDate.HasValue && date > endDate.Value)
            {
                continue;
            }

            yield return new KeyValuePair<DateOnly, decimal>(date, value);
        }
    }

    /// <summary>
    /// Returns the CSV file path for the given FRED series ID.
    /// </summary>
    /// <param name="seriesId">The FRED series identifier (e.g., "DGS10").</param>
    /// <returns>The fully qualified file path.</returns>
    public string GetCsvFileName(string seriesId) =>
        Path.Combine(_dataDirectory, $"fred_{SanitizeForFileName(seriesId)}.csv");

    private static string SanitizeForFileName(string input)
    {
        var invalidChars = new HashSet<char>(
            Path.GetInvalidFileNameChars()
                .Concat(['<', '>', ':', '"', '|', '?', '*', '\\', '/']));
        return new string(input.Where(c => !invalidChars.Contains(c)).ToArray());
    }
}
