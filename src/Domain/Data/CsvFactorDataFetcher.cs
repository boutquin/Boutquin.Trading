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
/// Fetches factor data from CSV files.
/// CSV format: Date,Factor1,Factor2,... (one dataset+frequency per file).
/// Named ff_{dataset}_{frequency}.csv (e.g., ff_ThreeFactors_daily.csv).
/// </summary>
public sealed class CsvFactorDataFetcher : IFactorDataFetcher
{
    private readonly string _dataDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvFactorDataFetcher"/> class.
    /// </summary>
    /// <param name="directory">Path to the directory containing Fama-French CSV files.</param>
    public CsvFactorDataFetcher(string directory)
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
    public IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchDailyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default) =>
        ReadCsvAsync(dataset, "daily", startDate, endDate, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchMonthlyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default) =>
        ReadCsvAsync(dataset, "monthly", startDate, endDate, cancellationToken);

    private async IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> ReadCsvAsync(
        FamaFrenchDataset dataset,
        string frequency,
        DateOnly? startDate,
        DateOnly? endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fileName = GetCsvFileName(dataset, frequency);

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"Factor data file not found for {dataset} {frequency}.", fileName);
        }

#pragma warning disable CA2007
        await using var fileStream = File.OpenRead(fileName);
#pragma warning restore CA2007
        using var streamReader = new StreamReader(fileStream);

        // Read header to get factor names
        var headerLine = await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (headerLine == null)
        {
            yield break;
        }

        var headers = headerLine.Split(',');
        // headers[0] is "Date", rest are factor names
        var factorNames = headers.Skip(1).ToArray();

        while (await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var columns = line.Split(',');

            DateOnly date;
            Dictionary<string, decimal> factors;
            try
            {
                date = DateOnly.Parse(columns[0], System.Globalization.CultureInfo.InvariantCulture);
                factors = new Dictionary<string, decimal>();
                for (var i = 0; i < factorNames.Length && i + 1 < columns.Length; i++)
                {
                    factors[factorNames[i]] = decimal.Parse(columns[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch (FormatException ex)
            {
                throw new MarketDataRetrievalException(
                    $"Error reading CSV file '{fileName}' for {dataset} {frequency}",
                    new InvalidDataException($"Invalid data format in CSV file '{fileName}'", ex));
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new MarketDataRetrievalException(
                    $"Error reading CSV file '{fileName}' for {dataset} {frequency}",
                    new InvalidDataException($"Invalid column count in CSV file '{fileName}'", ex));
            }

            if (startDate.HasValue && date < startDate.Value)
            {
                continue;
            }
            if (endDate.HasValue && date > endDate.Value)
            {
                continue;
            }

            yield return new KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>(date, factors);
        }
    }

    /// <summary>
    /// Returns the CSV file path for the given dataset and frequency.
    /// </summary>
    /// <param name="dataset">The Fama-French dataset identifier.</param>
    /// <param name="frequency">The data frequency (e.g., "daily", "monthly").</param>
    /// <returns>The fully qualified file path.</returns>
    public string GetCsvFileName(FamaFrenchDataset dataset, string frequency) =>
        Path.Combine(_dataDirectory, $"ff_{dataset}_{frequency}.csv");
}
