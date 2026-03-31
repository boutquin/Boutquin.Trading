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

namespace Boutquin.Trading.Domain.Data;

/// <summary>
/// Stores factor data to CSV files.
/// Uses atomic write (tmp + rename) since Fama-French data is overwrite-mode.
/// CSV format: Date,Factor1,Factor2,...
/// </summary>
public sealed class CsvFactorDataStorage
{
    private readonly string _dataDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvFactorDataStorage"/> class.
    /// </summary>
    /// <param name="directory">Path to the directory for storing Fama-French CSV files.</param>
    public CsvFactorDataStorage(string directory)
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

    /// <summary>
    /// Saves factor data to CSV with atomic write.
    /// Overwrites existing file (FF data is downloaded as complete dataset).
    /// </summary>
    public async Task SaveFactorsAsync(
        FamaFrenchDataset dataset,
        string frequency,
        IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var filePath = GetCsvFileName(dataset, frequency);
        var tmpPath = filePath + ".tmp";

        try
        {
            await using var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(fileStream);

            string[]? factorNames = null;

            await foreach (var kvp in data.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                // Write header on first row (factor names come from the data)
                if (factorNames == null)
                {
                    factorNames = kvp.Value.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
                    await writer.WriteLineAsync("Date," + string.Join(",", factorNames)).ConfigureAwait(false);
                }

                var values = factorNames.Select(f =>
                    kvp.Value.TryGetValue(f, out var v) ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
                var line = kvp.Key.ToString("O") + "," + string.Join(",", values);
                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }

            // Edge case: empty data — still write header-only file to mark as cached
            if (factorNames == null)
            {
                await writer.WriteLineAsync("Date").ConfigureAwait(false);
            }
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }
            throw;
        }

        File.Move(tmpPath, filePath, overwrite: true);
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
