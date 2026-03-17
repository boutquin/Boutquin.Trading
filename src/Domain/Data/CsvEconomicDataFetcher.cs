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

            DateOnly date;
            decimal value;
            try
            {
                date = DateOnly.Parse(columns[0], System.Globalization.CultureInfo.InvariantCulture);
                value = decimal.Parse(columns[1], System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (FormatException ex)
            {
                throw new MarketDataRetrievalException(
                    $"Error reading CSV file '{fileName}' for series '{seriesId}'",
                    new InvalidDataException($"Invalid data format in CSV file '{fileName}'", ex));
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new MarketDataRetrievalException(
                    $"Error reading CSV file '{fileName}' for series '{seriesId}'",
                    new InvalidDataException($"Invalid column count in CSV file '{fileName}'", ex));
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
